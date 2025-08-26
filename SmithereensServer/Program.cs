using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using SmithereensServer.Data;
using SmithereensServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmithereensServer.Services;
using File = System.IO.File;

class Program
{
    private static readonly ConcurrentDictionary<int, (string username, NetworkStream stream)> activeUserConnections =
        new ConcurrentDictionary<int, (string, NetworkStream)>();
    
    static EncryptionService encryptionService = new EncryptionService();

    static async Task Main(string[] args)
    {
        try
        {
            await using (var db = new SmithereensDbContext())
            {
                try
                {
                    var userCount = db.Users.Count();
                    Console.WriteLine($"Database connected. Total users: {userCount}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database connection failed: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return;
                }
            }

            var listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            Console.WriteLine("Server started on port 5000 at {0}", DateTime.Now);

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Client connected at {0}", DateTime.Now);
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server failed to start: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    // Обработка аутентификации
    static async Task HandleClientAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        var requestJson = await ReadRequestAsync(stream);
        if (requestJson == null) return;

        var request = JsonConvert.DeserializeObject<dynamic>(requestJson);
        string requestType = request.Type;

        // Проверяем тип запроса
        if (requestType == "LOGIN" || requestType == "REGISTER")
        {
            bool success = false;
            int userId = -1;
            string username = "";

            // Если запрос - вход
            if (requestType == "LOGIN")
            {
                string reqUsername = request.Username?.ToString();
                string reqPassword = request.Password?.ToString();

                using var db = new SmithereensDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Username == reqUsername);
                if (user != null && VerifyPassword(reqPassword, user.PasswordHash, user.Salt))
                {
                    success = true;
                    userId = user.UserID;
                    username = user.Username;
                }
            }
            // Если запрос - регистрация
            else if (requestType == "REGISTER")
            {
                string reqUsername = request.Username?.ToString();
                string passwordHash = request.PasswordHash?.ToString();
                string salt = request.Salt?.ToString();
                string profilePicture = request.ProfilePicture?.ToString();

                using var db = new SmithereensDbContext();
                if (!await db.Users.AnyAsync(u => u.Username == reqUsername))
                {
                    var user = new User
                    {
                        Username = reqUsername,
                        PasswordHash = passwordHash,
                        Salt = salt,
                        ProfilePicture = profilePicture
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                    success = true;
                    userId = user.UserID;
                    username = user.Username;
                }
            }

            if (success)
            {
                activeUserConnections[userId] = (username, stream);
                var response = new { Success = true, UserID = userId };
                await SendResponseAsync(stream, response);
                await HandleClientRequestsAsync(client, stream, userId);
            }
            else
            {
                var response = new { Success = false, ErrorMessage = "Authentication failed" };
                await SendResponseAsync(stream, response);
                client.Close();
            }
        }
    }

    static async Task HandleClientRequestsAsync(TcpClient client, NetworkStream stream, int userId)
    {
        try
        {
            while (true)
            {
                var requestJson = await ReadRequestAsync(stream);
                if (requestJson == null) break;

                var request = JsonConvert.DeserializeObject<dynamic>(requestJson);
                string type = request.Type;

                // Пользователь отправил сообщение
                if (type == "SEND_MESSAGE")
                {
                    var msgJson = request.Message.ToString();
                    MessageModel msgModel = JsonConvert.DeserializeObject<MessageModel>(msgJson);
                    Message dbMessage = new Message
                    {
                        ConversationID = msgModel.ConversationID,
                        SenderID = msgModel.SenderID,
                        Content = msgModel.Message,
                        Timestamp = msgModel.MessageTime,
                        IsEdited = false,
                        IsDeleted = false,
                        IsFileAttached = msgModel.IsFileAttached
                    };
                    using (var db = new SmithereensDbContext())
                    {
                        db.Messages.Add(dbMessage);
                        await db.SaveChangesAsync();
                    }
                    using (var db = new SmithereensDbContext())
                    {
                        var participantIds = await db.ConversationParticipants
                            .Where(cp => cp.ConversationID == dbMessage.ConversationID && cp.UserID != userId)
                            .Select(cp => cp.UserID)
                            .ToListAsync();

                        foreach (var participantId in participantIds)
                        {
                            if (activeUserConnections.TryGetValue(participantId, out var participantConnection))
                            {
                                var notification = new
                                {
                                    Type = "NEW_MESSAGE",
                                    Message = msgModel
                                };
                                await SendResponseAsync(participantConnection.stream, notification);
                            }
                        }
                    }

                    var successResponse = new { Success = true, dbMessage.MessageID };
                    await SendResponseAsync(stream, successResponse);
                }
                // Пользователь отправил файл
                else if (type == "SEND_FILE") 
                {   
                    try
                    {
                        // Десериализация данных
                        FileModel fileModel = JsonConvert.DeserializeObject<FileModel>(request.File.ToString());
                        MessageModel msgModel = JsonConvert.DeserializeObject<MessageModel>(request.Message.ToString());

                        // Проверка наличия данных файла
                        if (string.IsNullOrWhiteSpace(fileModel.FileBytes))
                        {
                            await SendResponseAsync(stream, new { Success = false, Error = "File content is missing" });
                            continue;
                        }

                        // Конвертация из Base64
                        byte[] fileBytes = Convert.FromBase64String(fileModel.FileBytes);
        
                        string downloadsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                        Directory.CreateDirectory(downloadsPath); // Гарантированное создание директории

                        string filePath = Path.Combine(downloadsPath, fileModel.FileName);
                        await File.WriteAllBytesAsync(filePath, fileBytes);

                        using (var db = new SmithereensDbContext())
                        {
                            // Сохранение метаданных файла
                            var fileDb = new SmithereensServer.Models.File
                            {
                                FileName = fileModel.FileName,
                                FileType = fileModel.FileType,
                                FileSize = fileBytes.Length, // Реальный размер
                                UploadTimestamp = DateTime.Now
                            };
                            db.Files.Add(fileDb);
                            await db.SaveChangesAsync();

                            // Создание сообщения
                            Message dbMessage = new Message
                            {
                                ConversationID = msgModel.ConversationID,
                                SenderID = msgModel.SenderID,
                                Content = msgModel.Message,
                                Timestamp = DateTime.Now, // Серверное время
                                IsEdited = false,
                                IsDeleted = false,
                                IsFileAttached = true,
                                FileId = fileDb.FileId
                            };
                                db.Messages.Add(dbMessage);
                                await db.SaveChangesAsync();

                                // Обновление модели для рассылки
                                msgModel.MessageID = dbMessage.MessageID;
                                msgModel.FileId = fileDb.FileId;
                                msgModel.FileName = fileModel.FileName;
                                msgModel.FileSize = fileBytes.Length;
                        }

                        // Отправка подтверждения
                        await SendResponseAsync(stream, new { Success = true });

                        // Оповещение участников
                        using (var db = new SmithereensDbContext())
                        {
                            var participantIds = await db.ConversationParticipants
                            .Where(cp => cp.ConversationID == msgModel.ConversationID && cp.UserID != userId)
                            .Select(cp => cp.UserID)
                            .ToListAsync();

                            foreach (var participantId in participantIds)
                            {
                                if (activeUserConnections.TryGetValue(participantId, out var conn))
                                {
                                    await SendResponseAsync(conn.stream, new
                                    {
                                        Type = "NEW_MESSAGE",
                                        Message = msgModel
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"File processing error: {ex}");
                        await SendResponseAsync(stream, new { Success = false, Error = ex.Message });
                    }
                }
                else if (type == "GET_CONVERSATIONS")
                {
                    try
                    {
                        int reqUserID = (int)request.UserID;
                        using var db = new SmithereensDbContext();

                        var convs = await db.Conversations
                            .Include(c => c.ConversationParticipants)
                            .Where(c => c.ConversationParticipants.Any(cp => cp.UserID == reqUserID))
                            .ToListAsync();

                        List<object> convList = new List<object>();
                        foreach (var c in convs)
                        {
                            // 1. Загружаем только ID файлов из сообщений
                            var fileIdsInMessages = await db.Messages
                                .Where(m => m.ConversationID == c.ConversationID && m.FileId != null)
                                .Select(m => m.FileId.Value)
                                .Distinct()
                                .ToListAsync();

                            // 2. Загружаем только необходимые файлы
                            var files = await db.Files
                                .Where(f => fileIdsInMessages.Contains(f.FileId))
                                .Select(f => new 
                                {
                                    f.FileId,
                                    f.FileName,
                                    f.FileSize
                                })
                                .ToDictionaryAsync(f => f.FileId);

                            // 3. Исправленный запрос сообщений с LEFT JOIN
                            var messages = await db.Messages
                                .Include(m => m.Sender)
                                .Where(m => m.ConversationID == c.ConversationID)
                                .OrderBy(m => m.Timestamp)
                                .Select(m => new 
                                {
                                    m.ConversationID,
                                    m.SenderID,
                                    Message = m.Content,
                                    MessageTime = m.Timestamp,
                                    m.IsEdited,
                                    m.IsDeleted,
                                    m.MessageID,
                                    IsFileAttached = m.FileId != null,
                                    FileId = m.FileId ?? 0,
                                    // Используем данные из JOIN
                                    FileName = m.FileId != null ? files.ContainsKey(m.FileId.Value) ? files[m.FileId.Value].FileName : null : null,
                                    FileSize = m.FileId != null ? files.ContainsKey(m.FileId.Value) ? files[m.FileId.Value].FileSize : 0 : 0
                                })
                                .ToListAsync();
                            
                            User? user = await db.Users.FirstOrDefaultAsync(u => u.UserID == reqUserID);
                            string? userName = user.Username;
                            
                            string? convName = c.ConversationName
                                .Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries)
                                .First(x => x != userName);
                            
                            convList.Add(new
                            {
                                ConvID = c.ConversationID,
                                ConvName = convName,
                                ImageSource = "",
                                Messages = messages
                            });
                        }

                        Console.WriteLine(
                            $"Sending conversations to user {reqUserID}: {JsonConvert.SerializeObject(convList)}");

                        var response = new { Success = true, Conversations = convList };
                        await SendResponseAsync(stream, response);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Error while getting conversations: {ex.Message}"); // Посмотреть, что реально пришло
                    }
                }
                else if (type == "DOWNLOAD_FILE")
                {
                    try
                    {
                        int fileId = (int)request.FileId;
                        using var db = new SmithereensDbContext();
                        var fileRecord = await db.Files.FindAsync(fileId);
        
                        if (fileRecord == null)
                        {
                            await SendResponseAsync(stream, new { Success = false, Error = "File not found" });
                            return;
                        }

                        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", fileRecord.FileName);

                        if (!File.Exists(filePath))
                        {
                            await SendResponseAsync(stream, new { Success = false, Error = "File not found on disk" });
                            return;
                        }

                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        string base64File = Convert.ToBase64String(fileBytes);

                        await SendResponseAsync(stream, new { 
                            Success = true, 
                            FileBytes = base64File,
                            FileName = fileRecord.FileName,
                            FileType = fileRecord.FileType
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"File download error: {ex.Message}");
                        await SendResponseAsync(stream, new { Success = false, Error = ex.Message });
                    }
                }
            }
        }
        finally
        {
            activeUserConnections.TryRemove(userId, out _);
            client.Close();
        }
    }

    static async Task<string> ReadRequestAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[30 * 1024 * 1024];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        if (bytesRead == 0) return null;
        string encryptedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        try
        {
            string decryptedMessage = encryptionService.Decrypt(encryptedMessage);
            return decryptedMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Decryption error: " + ex.Message);
            return null;
        }
    }

    static async Task SendResponseAsync(NetworkStream stream, object response)
    {
        var json = JsonConvert.SerializeObject(response);
        string encrypted = encryptionService.Encrypt(json);
        byte[] bytes = Encoding.UTF8.GetBytes(encrypted);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }

    static bool VerifyPassword(string password, string hash, string salt)
    {
        return hash == password + salt;
    }
}


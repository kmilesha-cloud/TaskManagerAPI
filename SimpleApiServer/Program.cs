using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace SimpleApiServer
{
    internal class Program
    {
        static List<TaskItem> _tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "Сделать лабораторную",
                Description = "Подготовить API на C#",
                IsCompleted = false,
                Priority = 2
            },
            new TaskItem
            {
                Id = 2,
                Title = "Проверить почту",
                Description = "Посмотреть сообщения от преподавателя",
                IsCompleted = true,
                Priority = 1
            },
            new TaskItem
            {
                Id = 3,
                Title = "Подготовить отчет",
                Description = "Оформить результаты лабораторной работы",
                IsCompleted = false,
                Priority = 3
            }
        };

        static List<User> _users = new List<User>();

        static string JwtSecret = "super_secret_key_12345_super_secret_key";
        static string JwtIssuer = "SimpleApiServer";
        static string JwtAudience = "SimpleApiServerClient";

        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();

            Console.WriteLine("Сервер запущен: http://localhost:5000/");
            Console.WriteLine("Endpoint: GET /api/tasks");
            Console.WriteLine("Endpoint: GET /api/tasks/{id}");
            Console.WriteLine("Endpoint: POST /api/tasks");
            Console.WriteLine("Endpoint: PUT /api/tasks/{id}");
            Console.WriteLine("Endpoint: POST /api/auth/register");
            Console.WriteLine("Endpoint: POST /api/auth/login");
            Console.WriteLine("Для остановки нажмите Ctrl + C");

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string path = request.Url.AbsolutePath;
                string method = request.HttpMethod;

                Console.WriteLine("Запрос: " + method + " " + path);

                try
                {
                    if (method == "GET" && path == "/api/tasks")
                    {
                        HandleGetTasks(request, response);
                    }
                    else if (method == "GET" && path.StartsWith("/api/tasks/"))
                    {
                        string idPart = path.Substring("/api/tasks/".Length);
                        int id;

                        if (int.TryParse(idPart, out id))
                        {
                            HandleGetTaskById(response, id);
                        }
                        else
                        {
                            WriteError(response, 400, "Неверный id", "INVALID_ID");
                        }
                    }
                    else if (method == "POST" && path == "/api/tasks")
                    {
                        HandleCreateTask(request, response);
                    }
                    else if (method == "PUT" && path.StartsWith("/api/tasks/"))
                    {
                        string idPart = path.Substring("/api/tasks/".Length);
                        int id;

                        if (int.TryParse(idPart, out id))
                        {
                            HandleUpdateTask(request, response, id);
                        }
                        else
                        {
                            WriteError(response, 400, "Неверный id", "INVALID_ID");
                        }
                    }
                    else if (method == "POST" && path == "/api/auth/register")
                    {
                        HandleRegister(request, response);
                    }
                    else if (method == "POST" && path == "/api/auth/login")
                    {
                        HandleLogin(request, response);
                    }
                    else
                    {
                        WriteError(response, 404, "Маршрут не найден", "ROUTE_NOT_FOUND");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка сервера: " + ex);

                    var result = new
                    {
                        error = "InternalServerError",
                        message = "Произошла внутренняя ошибка сервера"
                    };

                    string json = JsonSerializer.Serialize(result, GetJsonOptions());
                    WriteJsonResponse(response, json, 500);
                }
            }
        }

        static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        static void HandleGetTasks(HttpListenerRequest request, HttpListenerResponse response)
        {
            List<TaskItem> filteredTasks = new List<TaskItem>(_tasks);

            string isCompletedParam = request.QueryString["isCompleted"];
            string priorityParam = request.QueryString["priority"];

            if (!string.IsNullOrEmpty(isCompletedParam))
            {
                bool isCompletedValue;

                if (bool.TryParse(isCompletedParam, out isCompletedValue))
                {
                    List<TaskItem> result = new List<TaskItem>();

                    foreach (TaskItem task in filteredTasks)
                    {
                        if (task.IsCompleted == isCompletedValue)
                        {
                            result.Add(task);
                        }
                    }

                    filteredTasks = result;
                }
                else
                {
                    WriteError(response, 400, "Параметр isCompleted должен быть true или false", "INVALID_ISCOMPLETED");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(priorityParam))
            {
                int priorityValue;

                if (int.TryParse(priorityParam, out priorityValue))
                {
                    List<TaskItem> result = new List<TaskItem>();

                    foreach (TaskItem task in filteredTasks)
                    {
                        if (task.Priority == priorityValue)
                        {
                            result.Add(task);
                        }
                    }

                    filteredTasks = result;
                }
                else
                {
                    WriteError(response, 400, "Параметр priority должен быть числом", "INVALID_PRIORITY");
                    return;
                }
            }

            WriteSuccess(response, filteredTasks, 200);
        }

        static void HandleGetTaskById(HttpListenerResponse response, int id)
        {
            TaskItem foundTask = null;

            foreach (TaskItem task in _tasks)
            {
                if (task.Id == id)
                {
                    foundTask = task;
                    break;
                }
            }

            if (foundTask != null)
            {
                WriteSuccess(response, foundTask, 200);
            }
            else
            {
                WriteError(response, 404, "Задача не найдена", "TASK_NOT_FOUND");
            }
        }

        static void HandleCreateTask(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!TryValidateToken(request))
            {
                WriteUnauthorized(response, "Требуется авторизация");
                return;
            }

            string requestBody = ReadRequestBody(request);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            CreateTaskRequest newTaskRequest;
            try
            {
                newTaskRequest = JsonSerializer.Deserialize<CreateTaskRequest>(requestBody, GetJsonOptions());
            }
            catch
            {
                WriteError(response, 400, "Некорректный JSON", "INVALID_JSON");
                return;
            }

            if (newTaskRequest == null)
            {
                WriteError(response, 400, "Не удалось прочитать данные задачи", "INVALID_TASK");
                return;
            }

            List<object> validationErrors = ValidateTaskRequest(newTaskRequest);

            if (validationErrors.Count > 0)
            {
                WriteValidationError(response, validationErrors);
                return;
            }

            int newId = 1;

            if (_tasks.Count > 0)
            {
                newId = _tasks[_tasks.Count - 1].Id + 1;
            }

            TaskItem newTask = new TaskItem
            {
                Id = newId,
                Title = newTaskRequest.Title.Trim(),
                Description = newTaskRequest.Description,
                IsCompleted = newTaskRequest.IsCompleted ?? false,
                Priority = newTaskRequest.Priority ?? 1
            };

            _tasks.Add(newTask);

            WriteSuccess(response, newTask, 201);
        }

        static void HandleUpdateTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            string requestBody = ReadRequestBody(request);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            TaskItem foundTask = null;

            foreach (TaskItem task in _tasks)
            {
                if (task.Id == id)
                {
                    foundTask = task;
                    break;
                }
            }

            if (foundTask == null)
            {
                WriteError(response, 404, "Задача не найдена", "TASK_NOT_FOUND");
                return;
            }

            CreateTaskRequest updateTaskRequest;
            try
            {
                updateTaskRequest = JsonSerializer.Deserialize<CreateTaskRequest>(requestBody, GetJsonOptions());
            }
            catch
            {
                WriteError(response, 400, "Некорректный JSON", "INVALID_JSON");
                return;
            }

            if (updateTaskRequest == null)
            {
                WriteError(response, 400, "Не удалось прочитать данные задачи", "INVALID_TASK");
                return;
            }

            List<object> validationErrors = ValidateTaskRequest(updateTaskRequest);

            if (validationErrors.Count > 0)
            {
                WriteValidationError(response, validationErrors);
                return;
            }

            foundTask.Title = updateTaskRequest.Title.Trim();
            foundTask.Description = updateTaskRequest.Description;
            foundTask.IsCompleted = updateTaskRequest.IsCompleted ?? false;
            foundTask.Priority = updateTaskRequest.Priority ?? 1;

            WriteSuccess(response, foundTask, 200);
        }

        static void HandleRegister(HttpListenerRequest request, HttpListenerResponse response)
        {
            string requestBody = ReadRequestBody(request);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            AuthRequest registerRequest;
            try
            {
                registerRequest = JsonSerializer.Deserialize<AuthRequest>(requestBody, GetJsonOptions());
            }
            catch
            {
                WriteError(response, 400, "Некорректный JSON", "INVALID_JSON");
                return;
            }

            if (registerRequest == null ||
                string.IsNullOrWhiteSpace(registerRequest.Email) ||
                string.IsNullOrWhiteSpace(registerRequest.Password))
            {
                WriteError(response, 400, "Email и Password обязательны", "INVALID_REGISTER_DATA");
                return;
            }

            foreach (User user in _users)
            {
                if (user.Email.ToLower() == registerRequest.Email.ToLower())
                {
                    WriteError(response, 400, "Пользователь с таким email уже существует", "EMAIL_ALREADY_EXISTS");
                    return;
                }
            }

            int newId = _users.Count + 1;

            User newUser = new User
            {
                Id = newId,
                Email = registerRequest.Email,
                PasswordHash = HashPassword(registerRequest.Password),
                Name = registerRequest.Name
            };

            _users.Add(newUser);

            WriteSuccess(response, new
            {
                message = "Пользователь зарегистрирован",
                email = newUser.Email
            }, 201);
        }

        static void HandleLogin(HttpListenerRequest request, HttpListenerResponse response)
        {
            string requestBody = ReadRequestBody(request);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            AuthRequest loginRequest;
            try
            {
                loginRequest = JsonSerializer.Deserialize<AuthRequest>(requestBody, GetJsonOptions());
            }
            catch
            {
                WriteError(response, 400, "Некорректный JSON", "INVALID_JSON");
                return;
            }

            if (loginRequest == null ||
                string.IsNullOrWhiteSpace(loginRequest.Email) ||
                string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                WriteUnauthorized(response, "Неверный email или пароль");
                return;
            }

            User foundUser = null;

            foreach (User user in _users)
            {
                if (user.Email.ToLower() == loginRequest.Email.ToLower())
                {
                    foundUser = user;
                    break;
                }
            }

            if (foundUser == null)
            {
                WriteUnauthorized(response, "Неверный email или пароль");
                return;
            }

            string passwordHash = HashPassword(loginRequest.Password);

            if (foundUser.PasswordHash != passwordHash)
            {
                WriteUnauthorized(response, "Неверный email или пароль");
                return;
            }

            DateTime expiresAt;
            string token = GenerateJwtToken(foundUser, out expiresAt);

            var result = new
            {
                token = token,
                email = foundUser.Email,
                expiresAt = expiresAt
            };

            string json = JsonSerializer.Serialize(result, GetJsonOptions());
            WriteJsonResponse(response, json, 200);
        }

        static string ReadRequestBody(HttpListenerRequest request)
        {
            using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        static List<object> ValidateTaskRequest(CreateTaskRequest taskRequest)
        {
            List<object> errors = new List<object>();

            if (string.IsNullOrWhiteSpace(taskRequest.Title))
            {
                errors.Add(new
                {
                    field = "Title",
                    message = "Название обязательно"
                });
            }
            else if (taskRequest.Title.Trim().Length > 200)
            {
                errors.Add(new
                {
                    field = "Title",
                    message = "Название не должно превышать 200 символов"
                });
            }

            if (!string.IsNullOrEmpty(taskRequest.Description) && taskRequest.Description.Length > 1000)
            {
                errors.Add(new
                {
                    field = "Description",
                    message = "Описание не должно превышать 1000 символов"
                });
            }

            if (!taskRequest.Priority.HasValue)
            {
                errors.Add(new
                {
                    field = "Priority",
                    message = "Приоритет обязателен"
                });
            }
            else if (taskRequest.Priority.Value < 1 || taskRequest.Priority.Value > 3)
            {
                errors.Add(new
                {
                    field = "Priority",
                    message = "Приоритет должен быть в диапазоне от 1 до 3"
                });
            }

            return errors;
        }

        static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        static string GenerateJwtToken(User user, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddHours(1);

            byte[] keyBytes = Encoding.UTF8.GetBytes(JwtSecret);
            SymmetricSecurityKey key = new SymmetricSecurityKey(keyBytes);
            SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            Claim[] claims = new Claim[]
            {
                new Claim("id", user.Id.ToString()),
                new Claim("email", user.Email)
            };

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: JwtIssuer,
                audience: JwtAudience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        static bool TryValidateToken(HttpListenerRequest request)
        {
            string authHeader = request.Headers["Authorization"];

            if (string.IsNullOrWhiteSpace(authHeader))
            {
                return false;
            }

            if (!authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            string token = authHeader.Substring("Bearer ".Length).Trim();

            try
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(JwtSecret);

                TokenValidationParameters parameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(token, parameters, out _);

                return true;
            }
            catch
            {
                return false;
            }
        }

        static void WriteValidationError(HttpListenerResponse response, List<object> errors)
        {
            var result = new
            {
                error = "Ошибка валидации",
                errors = errors
            };

            string json = JsonSerializer.Serialize(result, GetJsonOptions());
            WriteJsonResponse(response, json, 400);
        }

        static void WriteUnauthorized(HttpListenerResponse response, string message)
        {
            var result = new
            {
                error = "Unauthorized",
                message = message
            };

            string json = JsonSerializer.Serialize(result, GetJsonOptions());
            WriteJsonResponse(response, json, 401);
        }

        static void WriteJsonResponse(HttpListenerResponse response, string json, int statusCode)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        static void WriteSuccess(HttpListenerResponse response, object data, int statusCode)
        {
            var result = new
            {
                data = data,
                error = (object)null
            };

            string json = JsonSerializer.Serialize(result, GetJsonOptions());
            WriteJsonResponse(response, json, statusCode);
        }

        static void WriteError(HttpListenerResponse response, int statusCode, string message, string code)
        {
            var result = new
            {
                data = (object)null,
                error = new
                {
                    message = message,
                    code = code
                }
            };

            string json = JsonSerializer.Serialize(result, GetJsonOptions());
            WriteJsonResponse(response, json, statusCode);
        }
    }
}
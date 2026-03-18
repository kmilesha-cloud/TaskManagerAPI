using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

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
            Console.WriteLine("Для остановки нажмите Ctrl + C");

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string path = request.Url.AbsolutePath;
                string method = request.HttpMethod;

                Console.WriteLine("Запрос: " + method + " " + path);

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
                else
                {
                    WriteError(response, 404, "Маршрут не найден", "ROUTE_NOT_FOUND");
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
            string requestBody = ReadRequestBody(request);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            try
            {
                CreateTaskRequest newTaskRequest = JsonSerializer.Deserialize<CreateTaskRequest>(requestBody, GetJsonOptions());

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
            catch
            {
                WriteError(response, 400, "Некорректный JSON", "INVALID_JSON");
            }
        }

        static void HandleUpdateTask(HttpListenerRequest request, HttpListenerResponse response, int id)
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

            if (foundTask == null)
            {
                WriteError(response, 404, "Задача не найдена", "TASK_NOT_FOUND");
                return;
            }

            string requestBody = ReadRequestBody(request);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            try
            {
                CreateTaskRequest updateTaskRequest = JsonSerializer.Deserialize<CreateTaskRequest>(requestBody, GetJsonOptions());

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
            catch
            {
                WriteError(response, 400, "Некорректный JSON", "INVALID_JSON");
            }
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

        static void WriteJsonResponse(HttpListenerResponse response, string json, int statusCode)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        static void WriteSuccess(HttpListenerResponse response, object data, int statusCode = 200)
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
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
            Stream body = request.InputStream;
            string requestBody = "";

            using (StreamReader reader = new StreamReader(body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                WriteError(response, 400, "Пустое тело запроса", "EMPTY_BODY");
                return;
            }

            try
            {
                TaskItem newTask = JsonSerializer.Deserialize<TaskItem>(requestBody, GetJsonOptions());

                if (newTask == null)
                {
                    WriteError(response, 400, "Не удалось прочитать данные задачи", "INVALID_TASK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(newTask.Title))
                {
                    WriteError(response, 400, "Поле Title обязательно", "TITLE_REQUIRED");
                    return;
                }

                if (newTask.Priority < 1 || newTask.Priority > 3)
                {
                    WriteError(response, 400, "Поле Priority должно быть в диапазоне от 1 до 3", "INVALID_PRIORITY_RANGE");
                    return;
                }

                int newId = 1;

                if (_tasks.Count > 0)
                {
                    newId = _tasks[_tasks.Count - 1].Id + 1;
                }

                newTask.Id = newId;
                _tasks.Add(newTask);

                WriteSuccess(response, newTask, 201);
            }
            catch
            {
                WriteError(response, 400, "Неверный JSON", "INVALID_JSON");
            }
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

        static void WriteTextResponse(HttpListenerResponse response, string text, int statusCode)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
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
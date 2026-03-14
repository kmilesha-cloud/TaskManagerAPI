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
            new TaskItem { Id = 1, Title = "Сделать лабораторную", Description = "Подготовить API на C#", IsCompleted = false },
            new TaskItem { Id = 2, Title = "Проверить почту", Description = "Посмотреть сообщения от преподавателя", IsCompleted = true }
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
                    HandleGetTasks(response);
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
                        WriteTextResponse(response, "Неверный id", 400);
                    }
                }
                else if (method == "POST" && path == "/api/tasks")
                {
                    HandleCreateTask(request, response);
                }
                else
                {
                    WriteTextResponse(response, "Маршрут не найден", 404);
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

        static void HandleGetTasks(HttpListenerResponse response)
        {
            string json = JsonSerializer.Serialize(_tasks, GetJsonOptions());
            WriteJsonResponse(response, json, 200);
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
                string json = JsonSerializer.Serialize(foundTask, GetJsonOptions());
                WriteJsonResponse(response, json, 200);
            }
            else
            {
                string json = JsonSerializer.Serialize(new { message = "Задача не найдена" }, GetJsonOptions());
                WriteJsonResponse(response, json, 404);
            }
        }

        static void HandleCreateTask(HttpListenerRequest request, HttpListenerResponse response)
        {
            Stream body = request.InputStream;
            Encoding encoding = request.ContentEncoding;

            string requestBody = "";

            using (StreamReader reader = new StreamReader(body, encoding))
            {
                requestBody = reader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                string errorJson = JsonSerializer.Serialize(new { message = "Пустое тело запроса" }, GetJsonOptions());
                WriteJsonResponse(response, errorJson, 400);
                return;
            }

            try
            {
                TaskItem newTask = JsonSerializer.Deserialize<TaskItem>(requestBody, GetJsonOptions());

                if (newTask == null || string.IsNullOrWhiteSpace(newTask.Title))
                {
                    string errorJson = JsonSerializer.Serialize(new { message = "Поле Title обязательно" }, GetJsonOptions());
                    WriteJsonResponse(response, errorJson, 400);
                    return;
                }

                int newId = 1;

                if (_tasks.Count > 0)
                {
                    newId = _tasks[_tasks.Count - 1].Id + 1;
                }

                newTask.Id = newId;
                _tasks.Add(newTask);

                string json = JsonSerializer.Serialize(newTask, GetJsonOptions());
                WriteJsonResponse(response, json, 201);
            }
            catch
            {
                string errorJson = JsonSerializer.Serialize(new { message = "Неверный JSON" }, GetJsonOptions());
                WriteJsonResponse(response, errorJson, 400);
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
    }
}
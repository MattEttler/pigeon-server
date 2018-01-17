using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Pigeon
{
    public class MessageConverter  
    {
        private readonly RequestDelegate _next;
        private string CorrelationId { get; set; }

        public MessageConverter(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                context.Request.ContentType = "application/json";

                string postData = new System.IO.StreamReader(context.Request.Body).ReadToEnd();
                var path = context.Request.Path.ToUriComponent().Split('/');
                await context.Response.WriteAsync(CreateMessage(path[1], path[2], postData));
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                await context.Response.WriteAsync("<h1>500 Internal Server Error</h1>");
            }            
        }

        //Transform to a packaged JSON message compatible with the Pigeon framework, then ship it.
        public string CreateMessage(string microservice, string action, string parameters)
        {
            this.CorrelationId = Guid.NewGuid().ToString();
            
            var factory = new ConnectionFactory();
            factory.HostName = Program._configuration["RabbitMQServer"];
            factory.UserName = Program._configuration["RabbitMQUser"];
            factory.Password = Program._configuration["RabbitMQPass"];
            factory.VirtualHost = Program._configuration["RabbitMQVirtualHost"];

            using( var connection = factory.CreateConnection())
            {
                using( var channel = connection.CreateModel())
                {
                    channel.ExchangeDeclare("PigeonExchange", ExchangeType.Topic, true);
                    
                    var jsonMessage = new JObject();
                    jsonMessage.Add("action", action);
                    jsonMessage.Add("parameters", parameters);

                    byte[] body = Encoding.UTF8.GetBytes(jsonMessage.ToString());
                    string replyQueue = channel.QueueDeclare().QueueName;
                    string route = String.Format("Pigeon.{0}.Requests", microservice.ToLower());

                    var props = channel.CreateBasicProperties();
                    props.ReplyTo = replyQueue;
                    props.CorrelationId = this.CorrelationId;

                    channel.BasicPublish(exchange: "PigeonExchange",
                                        routingKey: route,
                                        basicProperties: props,
                                        body: body);

                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queue: replyQueue,
                        noAck: true,
                        consumer: consumer);

                    while(true)
                    {
                        var ea = (BasicDeliverEventArgs)consumer.Queue.Dequeue();
                        if(ea.BasicProperties.CorrelationId == this.CorrelationId)
                        {
                            return Encoding.UTF8.GetString(ea.Body);
                        }
                    }
                }
            }
        }
    }
}
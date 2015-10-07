using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ContosoConf.Live
{
    class LiveClientConnection
    {
        static readonly QuestionList Questions = new QuestionList();

        readonly WebSocket socket;

        public LiveClientConnection(WebSocket socket)
        {
            this.socket = socket;
        }

        void SendQuestions(IEnumerable<Question> questions)
        {
            var message = new { questions };
            SendJsonMessage(message);
        }

        void SendRemove(int id)
        {
            SendJsonMessage(new { remove = id });
        }

        void SendJsonMessage(object message)
        {
            lock (socket)
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(message);
                var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
                socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
        }

        public async Task Start()
        {
            Questions.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        SendQuestions(args.NewItems.Cast<Question>());
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        {
                            var ids = args.OldItems.Cast<Question>().Select(q => q.id).ToList();
                            ids.ForEach(SendRemove);
                        }
                        break;
                }
            };

            if (Questions.Count == 0)
            {
                SimulateOtherClients();
            }
            else
            {
                SendQuestions(Questions);
            }

            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveMessage();
                if (message == null) continue;
              
                Questions.Add(new Question(message.text,message.remitente));
            }
        }

        void SimulateOtherClients()
        {
            var badQuestion = new Question(3, "This is an #&!%!* inappropriate message!!");

            Task.Delay(250)
                .ContinueWith(_ => Questions.Add(
                    new Question(1, "What are some good resources for getting started with HTML5?")
                ));

            Task.Delay(1000)
                .ContinueWith(_ =>
                {
                    Questions.Add(new Question(2, "Can you explain more about the Web Socket API please?"));
                    Questions.Add(badQuestion);
                });

            Task.Delay(3000)
                .ContinueWith(_ => Questions.Remove(badQuestion));

            Task.Delay(4000)
                .ContinueWith(_ => Questions.Add(
                    new Question(4, "How much of CSS3 can I use right now?")
                ));
        }

     

        async Task<Question> ReceiveMessage()
        {
            var buffer = new ArraySegment<byte>(new byte[1024]);
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, result.Count);
            var serializer = new JavaScriptSerializer();
            try
            {
                var obj = serializer.Deserialize<Question>(json);
                return obj;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        class Question
        {
            static int _nextId = 1000;
            public Question() { }
            public Question(string text)
            {
                id = Interlocked.Increment(ref _nextId);
                this.text = text;
            }

            public Question(int id, string text)
            {
                this.id = id;
                this.text = text;
                remitente = "Sistema";
            }
            
            public Question(string text, String remite)
            {
                id = Interlocked.Increment(ref _nextId);
                this.text = text;
                this.remitente = remite;
            }

            public string text { get; set; }
            public int id { get; set; }
            public string remitente { get; set; }
        }

        class QuestionList : ObservableCollection<Question>
        {
        }
    }
}
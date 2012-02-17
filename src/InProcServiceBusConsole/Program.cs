using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Castle.Core;
using Castle.Core.Configuration;
using Castle.Facilities.Startable;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;

namespace InProcServiceBusConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var container = new WindsorContainer())
            {
                container.AddFacility<StartableFacility>(); // can register afterward
                container.Register(
                    Component.For<Publisher>().ImplementedBy<Publisher>(), 
                    Component.For<IEventBus>().ImplementedBy<EventBus>(),
                    Component.For<Subscriber>().ImplementedBy<Subscriber>());

                Console.WriteLine("Press any key to stop");
                Console.ReadKey();
            }
        }
    }

    public interface IEventBus
    {
        void Publish<T>(T message);
        void AddConsumer<TMessage>(IConsume<TMessage> consumer);
    }

    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Action<object>>> _handlers = new Dictionary<Type, List<Action<object>>>();

        public void AddConsumer<TMessage>(IConsume<TMessage> consumer)
        {
            var messageType = typeof (TMessage);
            if(!_handlers.ContainsKey(messageType))
                _handlers.Add(messageType, new List<Action<object>>());

            var list = _handlers[messageType] as List<Action<object>>;
            list.Add(msg => consumer.Consume((TMessage)msg));
        }

        public void Publish<TMessage>(TMessage message)
        {
            if (!_handlers.ContainsKey(typeof(TMessage)))
                return;

            var list = _handlers[typeof (TMessage)] as List<Action<object>>;
            foreach (var consumerAction in list)
                consumerAction(message);
        }
    }

    public class Publisher: IStartable
    {
        private readonly IEventBus _eventBus;
        private Timer _timer;

        public Publisher(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void Start()
        {
            _timer = new Timer(_ => _TimerCallback());
            _timer.Change(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000));
        }

        private void _TimerCallback()
        {
            try
            {
                _eventBus.Publish(new SomeMessage {SomeValue = "hello"});
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _timer.Change(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
        }

        public void Stop()
        {
            _timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }
    }


    public class Subscriber : IConsume<SomeMessage>, IStartable
    {
        private readonly IEventBus _eventBus;

        public Subscriber(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void Consume(SomeMessage message)
        {
            Console.WriteLine("Got: {0}", message.SomeValue);
            throw new Exception("error");
        }

        public void Start()
        {
            _eventBus.AddConsumer<SomeMessage>(this);
        }

        public void Stop()
        {
            
        }
    }

    public interface IConsume<T>
    {
        void Consume(T message);
    }

    public class SomeMessage
    {
        public string SomeValue { get; set; }
    }
}

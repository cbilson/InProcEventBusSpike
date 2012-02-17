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
                container.AddFacility<EventBusWireUpFacility>();
                container.AddFacility<StartableFacility>(); // can register afterward
                container.Register(
                    Component.For<Publisher>().ImplementedBy<Publisher>(), 
                    Component.For<Subscriber>().ImplementedBy<Subscriber>());

                Console.WriteLine("Press any key to stop");
                Console.ReadKey();
            }
        }
    }

    internal class EventBusWireUpFacility : IFacility
    {
        public void Init(IKernel kernel, IConfiguration facilityConfig)
        {
            var eventBus = new EventBus();
            kernel.Register(Component.For<IEventBus>().Instance(eventBus));
            kernel.ComponentCreated += (model, instance) =>
                           {
                               instance.GetType().GetInterfaces()
                                   .Where(x => x.IsGenericType)
                                   .Where(x => x.GetGenericTypeDefinition() == typeof(IConsume<>))
                                   .Select(x => x.GetGenericArguments()[0])
                                   .ToList()
                                   .ForEach(x => eventBus.AddConsumer(x, instance));
                           };
        }

        public void Terminate()
        {
        }
    }

    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new Dictionary<Type, List<object>>();

        public void AddConsumer(Type messageType, object consumer)
        {
            if(!_handlers.ContainsKey(messageType))
                _handlers.Add(messageType, new List<object>());

            var list = _handlers[messageType];
            list.Add(consumer);
        }

        public void Publish<T>(T message)
        {
            if (!_handlers.ContainsKey(typeof(T)))
                return;

            var list = _handlers[typeof (T)];
            foreach (var consumer in list.Cast<IConsume<T>>())
                consumer.Consume(message);
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
            _eventBus.Publish(new SomeMessage {SomeValue = "hello"});
            _timer.Change(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));
        }

        public void Stop()
        {
            _timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }
    }

    public interface IEventBus
    {
        void Publish<T>(T message);
    }

    public class Subscriber : IConsume<SomeMessage>, IStartable
    {
        public void Consume(SomeMessage message)
        {
            Console.WriteLine("Got: {0}", message.SomeValue);
        }

        public void Start()
        {
            
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

﻿using System;
using SimpleInjector;
using Tapeti;
using Tapeti.DataAnnotations;
using Tapeti.Flow;
using Tapeti.SimpleInjector;
using System.Threading;
using Tapeti.Transient;

namespace Test
{
    internal class Program
    {
        private static void Main()
        {
            // TODO logging
            //try
            {
                var container = new Container();
                container.Register<MarcoEmitter>();
                container.Register<Visualizer>();
                container.Register<ILogger, Tapeti.Default.ConsoleLogger>();

                var config = new TapetiConfig(new SimpleInjectorDependencyResolver(container))
                    //.WithFlowSqlRepository("Server=localhost;Database=TapetiTest;Integrated Security=true")
                    .WithFlow()
                    .WithDataAnnotations()
                    .WithTransient(TimeSpan.FromSeconds(30))
                    .RegisterAllControllers()
                    //.DisablePublisherConfirms() -> you probably never want to do this if you're using Flow or want requeues when a publish fails
                    .Build();

                using (var connection = new TapetiConnection(config)
                {
                    Params = new TapetiAppSettingsConnectionParams()
                })
                {
                    var flowStore = container.GetInstance<IFlowStore>();
                    var flowStore2 = container.GetInstance<IFlowStore>();
                    Console.WriteLine("IFlowHandler is singleton = " + (flowStore == flowStore2));

                    flowStore.Load().Wait();

                    connection.Connected += (sender, e) => { Console.WriteLine("Event Connected"); };
                    connection.Disconnected += (sender, e) => { Console.WriteLine("Event Disconnected"); };
                    connection.Reconnected += (sender, e) => { Console.WriteLine("Event Reconnected"); };

                    Console.WriteLine("Subscribing...");
                    var subscriber = connection.Subscribe(false).Result;

                    Console.WriteLine("Consuming...");
                    subscriber.Resume().Wait();

                    Console.WriteLine("Done!");

                    /*
                    var response = container.GetInstance<ITransientPublisher>()
                        .RequestResponse<PoloConfirmationRequestMessage, PoloConfirmationResponseMessage>(
                            new PoloConfirmationRequestMessage
                            {
                                StoredInState = new Guid("309088d8-9906-4ef3-bc64-56976538d3ab")
                            }).Result;

                    Console.WriteLine(response.ShouldMatchState);
                    */

                    //connection.GetPublisher().Publish(new FlowEndController.PingMessage());

                    container.GetInstance<IFlowStarter>().Start<MarcoController, bool>(c => c.StartFlow, true).Wait();
                    container.GetInstance<IFlowStarter>().Start<MarcoController>(c => c.TestParallelRequest).Wait();

                    Thread.Sleep(1000);

                    //var emitter = container.GetInstance<MarcoEmitter>();
                    //emitter.Run().Wait();


                }
            }
            //catch (Exception e)
            {
            //    Console.WriteLine(e.ToString());
            //    Console.ReadKey();
            }
        }
    }
}

using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Tangle.Net.Cryptography;
using Tangle.Net.Entity;
using Tangle.Net.Mam.Entity;
using Tangle.Net.Mam.Merkle;
using Tangle.Net.Mam.Services;
using Tangle.Net.Repository;

namespace IotaEclassDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Call this once to create the property channel
                // and channels for the payable properties
                CreateMachineInfoChannel().Wait();
                // Call this to update the payable channels with new values
                CreateUpdateMachinePropertyChannel("outputVoltage1", "1").Wait();
                Console.WriteLine("Complete press any key to exit");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was an exception: {ex.ToString()}");
            }
        }

        static async Task CreateMachineInfoChannel()
        {
            var repository = new RestIotaRepository(new RestClient("https://nodes.devnet.iota.org:443"));
            var factory = new MamChannelFactory(CurlMamFactory.Default, CurlMerkleTreeFactory.Default, repository);
            MamChannel channel;

            if (!File.Exists("property-channel-mam-state.json")) {
                Console.WriteLine("Creating new mam state for property channel");
                var seed = Seed.Random();
                channel = factory.Create(Mode.Public, seed, SecurityLevel.Medium);
                Console.WriteLine($"Seed: {seed.Value}");
            }
            else
            {
                Console.WriteLine("Reading existing mam state for property channel");
                channel = factory.CreateFromJson(File.ReadAllText("property-channel-mam-state.json"));
            }

            var data = new MachineInfo();
            data.name = "Sprocket Machine";
            data.publicProperties = new PublicProperty[3] {
                new PublicProperty
                {
                    alias = "prodType",
                    eClassPropertyId = "0173-1#02-AAO057#002",
                    value = "Super Sprocket machine"
                },
                new PublicProperty
                {
                    alias = "prodDescription",
                    eClassPropertyId = "0173-1#02-AAU734#001",
                    value = "New fangled machine that does something"
                },
                new PublicProperty
                {
                    alias = "yearConstruction",
                    eClassPropertyId = "0173-1#02-AAP906#001",
                    value = "2019"
                }
            };
            data.payableProperties = new PayableProperty[1]
            {
                new PayableProperty
                {
                    alias = "outputVoltage1",
                    eClassPropertyId = "0173-1#02-AAM471#004",
                    fee = 1,
                    leaseTimeInMs = 120000,
                    root = ""
                }
            };

            for (var i = 0; i < data.payableProperties.Length; i++)
            {
                data.payableProperties[i].root = await CreateUpdateMachinePropertyChannel(data.payableProperties[i].alias, "0");
            }

            Console.WriteLine("Creating eCl@ss properties message");
            var message = channel.CreateMessage(TryteString.FromAsciiString(JsonConvert.SerializeObject(data)));
            Console.WriteLine($"Channel Root {message.Root}");
            Console.WriteLine("Publishing eCl@ss properties message");
            await channel.PublishAsync(message, 9, 3);

            Console.WriteLine("Writing mam state for property channel");
            File.WriteAllText("property-channel-mam-state.json", channel.ToJson());
        }

        static async Task<string> CreateUpdateMachinePropertyChannel(string alias, string value)
        {
            var repository = new RestIotaRepository(new RestClient("https://nodes.devnet.iota.org:443"));
            var factory = new MamChannelFactory(CurlMamFactory.Default, CurlMerkleTreeFactory.Default, repository);

            MamChannel channel;
            if (!File.Exists("property-channel-mam-state.json"))
            {
                Console.WriteLine($"{alias} Creating new mam state for payable channel");
                var seed = Seed.Random();
                channel = factory.Create(Mode.Public, seed, SecurityLevel.Medium);
                Console.WriteLine($"{alias} Seed: {seed.Value}");
            }
            else
            {
                Console.WriteLine($"{alias} Reading existing mam state for property channel");
                channel = factory.CreateFromJson(File.ReadAllText($"{alias}-mam-state.json"));
            }

            var data = new SubscriptionValue();
            data.value = value;

            Console.WriteLine($"{alias} Creating eCl@ss properties message");
            var message = channel.CreateMessage(TryteString.FromAsciiString(JsonConvert.SerializeObject(data)));
            Console.WriteLine($"{alias} Channel Root {message.Root}");
            Console.WriteLine($"{alias} Publishing eCl@ss properties message");
            await channel.PublishAsync(message, 9, 3);

            Console.WriteLine($"{alias} Writing mam state for property channel");
            File.WriteAllText($"{alias}-mam-state.json", channel.ToJson());

            return message.Root.ToString();
        }
    }
}

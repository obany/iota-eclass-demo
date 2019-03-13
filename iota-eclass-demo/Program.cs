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
                // Which node are we going to use for attaching, use devnet
                var repository = new RestIotaRepository(new RestClient("https://nodes.devnet.iota.org:443"));

                // Create a factory for generating mam channels
                var factory = new MamChannelFactory(CurlMamFactory.Default, CurlMerkleTreeFactory.Default, repository);

                // Call this once to create the property channel
                // and channels for the payable properties
                CreateMachineInfoChannel(factory).Wait();

                // Call this to update the payable channels with new values
                CreateUpdateMachinePropertyChannel(factory, "outputVoltage1", "1").Wait();

                Console.WriteLine("Complete press any key to exit");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was an exception: {ex.ToString()}");
            }
        }

        static async Task CreateMachineInfoChannel(MamChannelFactory factory)
        {
            MamChannel channel;

            // Try and load the current state of the mam channel from the config file
            if (!File.Exists("property-channel-mam-state.json")) {
                // File does not exist so generate a new channel
                Console.WriteLine("Creating new mam state for property channel");

                // Create a random seed
                var seed = Seed.Random();
                Console.WriteLine($"Seed: {seed.Value}");

                // Generate a new channel in public mode
                channel = factory.Create(Mode.Public, seed, SecurityLevel.Medium);
            }
            else
            {
                // The state config exists so just create the channel from the JSON
                Console.WriteLine("Reading existing mam state for property channel");
                channel = factory.CreateFromJson(File.ReadAllText("property-channel-mam-state.json"));
            }

            // Create the property info object to post in the eCl@ss property channel
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

            // For each of the payable channels we need to create a separate mam channel
            // to post its values in, the root of the payable channel can then be added
            // into the property info for the machine
            for (var i = 0; i < data.payableProperties.Length; i++)
            {
                data.payableProperties[i].root = await CreateUpdateMachinePropertyChannel(factory, data.payableProperties[i].alias, "0");
            }

            // Now the property info is complete we can create the message into the property mam channel
            Console.WriteLine("Creating eCl@ss properties message");
            var message = channel.CreateMessage(TryteString.FromAsciiString(JsonConvert.SerializeObject(data)));

            // The message root is the value that needs storing to be provided to anyone else
            // you want to start using your data
            Console.WriteLine($"Channel Root {message.Root}");

            // Attach the message to the tangle
            Console.WriteLine("Publishing eCl@ss properties message");
            await channel.PublishAsync(message, 9, 3);

            Console.WriteLine($"View online at https://devnet.thetangle.org/mam/{message.Root}");

            // The mam channel state would have been updated when we created/published the message
            // so store the details to reuse in the future
            Console.WriteLine("Writing mam state for property channel");
            File.WriteAllText("property-channel-mam-state.json", channel.ToJson());
        }

        static async Task<string> CreateUpdateMachinePropertyChannel(MamChannelFactory factory, string alias, string value)
        {
            MamChannel channel;
            // Does the config for the property channel exist
            if (!File.Exists($"{alias}-mam-state.json"))
            {
                // No so create a new channel for the property
                Console.WriteLine($"{alias} Creating new mam state for payable channel");

                // Create a random seed
                var seed = Seed.Random();
                Console.WriteLine($"{alias} Seed: {seed.Value}");

                // Generate a new channel in public mode
                // if we use restricted mode we would need a way to perform
                // a key exchange on the sideKey
                channel = factory.Create(Mode.Public, seed, SecurityLevel.Medium);
            }
            else
            {
                // The state config exists so just create the channel from the JSON
                Console.WriteLine($"{alias} Reading existing mam state for property channel");
                channel = factory.CreateFromJson(File.ReadAllText($"{alias}-mam-state.json"));
            }

            // Create the object that will contain the value for property
            var data = new SubscriptionValue();
            data.value = value;

            // Now the property value is complete we can create the message into the property value mam channel
            Console.WriteLine($"{alias} Creating eCl@ss properties message");
            var message = channel.CreateMessage(TryteString.FromAsciiString(JsonConvert.SerializeObject(data)));

            // The message root is already stored in the property info data so no need
            // to remember it separately
            Console.WriteLine($"{alias} Channel Root {message.Root}");

            // Attach the message to the tangle
            Console.WriteLine($"{alias} Publishing eCl@ss properties message");
            await channel.PublishAsync(message, 9, 3);

            Console.WriteLine($"{alias} View online at https://devnet.thetangle.org/mam/{message.Root}");

            // The mam channel state would have been updated when we created/published the message
            // so store the details to reuse in the future
            Console.WriteLine($"{alias} Writing mam state for property channel");
            File.WriteAllText($"{alias}-mam-state.json", channel.ToJson());

            // Return the root so if we are called from the creation of the property channel
            // it can be stored in the property info
            return message.Root.ToString();
        }
    }
}

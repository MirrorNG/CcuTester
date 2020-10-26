using System;
using System.Collections;
using Mirror.KCP;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Mirror.HeadlessBenchmark
{
    public class HeadlessBenchmark : MonoBehaviour
    {
        public NetworkManager networkManager;
        public GameObject MonsterPrefab;
        public GameObject PlayerPrefab;
        public string editorArgs;

        string[] cachedArgs;
        string port;

        long inTotal;
        long outTotal;

        void Start()
        {
            cachedArgs = Environment.GetCommandLineArgs();

            if (Application.isEditor)
            {
                cachedArgs = editorArgs.Split(' ');
            }

            HeadlessStart();

            NetworkDiagnostics.InMessageEvent += NetworkDiagnostics_InMessageEvent;
            NetworkDiagnostics.OutMessageEvent += NetworkDiagnostics_OutMessageEvent;

        }

        private void NetworkDiagnostics_OutMessageEvent(NetworkDiagnostics.MessageInfo obj)
        {
            outTotal++;
        }

        private void NetworkDiagnostics_InMessageEvent(NetworkDiagnostics.MessageInfo obj)
        {
            inTotal++;
        }

        private IEnumerator DisplayFramesPerSecons()
        {
            int previousFrameCount = Time.frameCount;
            long previousin = 0;
            long previousout = 0;

            while (true)
            {
                yield return new WaitForSeconds(1);
                int frameCount = Time.frameCount;
                int frames = frameCount - previousFrameCount;

                long inCount = inTotal - previousin;
                long outCount = outTotal - previousout;

                if (Application.isEditor)
                {
                    Debug.LogFormat("{0} FPS {1} inbound messages {2} outbound messages {2} clients", frames, inCount, outCount, networkManager.server.NumPlayers);
                }
                else
                {
                    Console.WriteLine("{0} FPS {1} inbound messages {2} outbound messages {2} clients", frames, inCount, outCount, networkManager.server.NumPlayers);
                }

                previousin = inTotal;
                previousout = outTotal;
                previousFrameCount = frameCount;
            }
        }

        void HeadlessStart()
        {
            //Try to find port
            port = GetArgValue("-port");

            //Try to find Transport
            ParseForTransport();

            //Server mode?
            ParseForServerMode();

            //Or client mode?
            StartClients().Forget();

            ParseForHelp();
        }

        void OnServerStarted()
        {
            StartCoroutine(DisplayFramesPerSecons());

            string monster = GetArgValue("-monster");
            if (!string.IsNullOrEmpty(monster))
            {
                for (int i = 0; i < int.Parse(monster); i++)
                    SpawnMonsters(i);
            }
        }

        void SpawnMonsters(int i)
        {
            GameObject monster = Instantiate(MonsterPrefab);
            monster.gameObject.name = $"Monster {i}";
            networkManager.server.Spawn(monster.gameObject);
        }

        async UniTask StartClient(int i, Transport transport, string networkAddress)
        {
            var clientGo = new GameObject($"Client {i}", typeof(NetworkClient));
            NetworkClient client = clientGo.GetComponent<NetworkClient>();
            client.Transport = transport;

            client.RegisterPrefab(MonsterPrefab);
            client.RegisterPrefab(PlayerPrefab);

            try
            {
                await client.ConnectAsync(networkAddress);
                client.Send(new AddPlayerMessage());

            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

        }

        void ParseForServerMode()
        {
            if (!string.IsNullOrEmpty(GetArg("-server")))
            {
                networkManager.server.Started.AddListener(OnServerStarted);
                networkManager.server.Authenticated.AddListener(conn => networkManager.server.SetClientReady(conn));
                _ = networkManager.server.ListenAsync();
                Console.WriteLine("Starting Server Only Mode");
            }
        }

        async UniTaskVoid StartClients()
        {
            string client = GetArg("-client");
            if (!string.IsNullOrEmpty(client))
            {
                //network address provided?
                string address = GetArgValue("-address");
                if (string.IsNullOrEmpty(address))
                {
                    address = "localhost";
                }

                //nested clients
                int clonesCount = 1;
                string clonesString = GetArgValue("-client");
                if (!string.IsNullOrEmpty(clonesString))
                {
                    clonesCount = int.Parse(clonesString);
                }

                Console.WriteLine("Starting {0} clients", clonesCount);

                // connect from a bunch of clients
                for (int i = 0; i < clonesCount; i++)
                {
                    await StartClient(i, networkManager.client.Transport, address);
                    await UniTask.Delay(500);

                    Debug.LogFormat("Started {0} clients", i + 1);
                }
            }
        }

        void ParseForHelp()
        {
            if (!string.IsNullOrEmpty(GetArg("-help")))
            {
                Console.WriteLine("--==MirrorNG HeadlessClients Benchmark==--");
                Console.WriteLine("Please start your standalone application with the -nographics and -batchmode options");
                Console.WriteLine("Also provide these arguments to control the autostart process:");
                Console.WriteLine("-server (will run in server only mode)");
                Console.WriteLine("-client 1234 (will run the specified number of clients)");
                Console.WriteLine("-transport tcp (transport to be used in test. add more by editing HeadlessBenchmark.cs)");
                Console.WriteLine("-address example.com (will run the specified number of clients)");
                Console.WriteLine("-port 1234 (port used by transport)");
                Console.WriteLine("-monster 100 (number of monsters to spawn on the server)");

                Application.Quit();
            }
        }

        void ParseForTransport()
        {
            string transport = GetArgValue("-transport");
            if (string.IsNullOrEmpty(transport) || transport.Equals("kcp"))
            {
                KcpTransport newTransport = networkManager.gameObject.AddComponent<KcpTransport>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Port = ushort.Parse(port);
                }
                networkManager.server.transport = newTransport;
                networkManager.client.Transport = newTransport;
            }

        }

        string GetArgValue(string name)
        {
            for (int i = 0; i < cachedArgs.Length; i++)
            {
                if (cachedArgs[i] == name && cachedArgs.Length > i + 1)
                {
                    return cachedArgs[i + 1];
                }
            }
            return null;
        }

        string GetArg(string name)
        {
            for (int i = 0; i < cachedArgs.Length; i++)
            {
                if (cachedArgs[i] == name)
                {
                    return cachedArgs[i];
                }
            }
            return null;
        }
    }
}

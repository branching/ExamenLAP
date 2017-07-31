using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Timers;
using System.Diagnostics;

namespace GeneradorTexto
{
    class Program
    {
        // Define la cantidad de textos aleatorios disponibles para ser enviados al servidor.
        private const int cantTextos = 10;
        
        // Estandarizar la codificación de texto que se va a manejar.
        private static Encoding codificacion = Encoding.GetEncoding("iso-8859-3");

        // Arreglo de textos aleatorios que se enviará al servidor por cliente.
        private static string[] arregloTextos;

        // Define el intervalo de milisegundos para el envío del texto aleatorio.
        private const double intervalo = 1;

        // Dirección url de la API REST/json.
        private static string url;

        private static Random genRandom;
        private static Timer temporiza;

        static void Main(string[] args)
        {
            try
            {
                // Se inicializa la dirección URL
                string servidor;
                string puerto;

                if (args.Length >= 2)
                {
                    servidor = args[0];
                    puerto = args[1];
                }
                else
                {
                    // Se inicializa la dirección URL.
                    Console.WriteLine(@"Ingrese la URL de la API a consumir (por defecto http://localhost):");
                    servidor = Console.ReadLine();
                    servidor = servidor.Length == 0 ? @"http://localhost" : servidor;

                    Console.WriteLine("Ingrese el puerto (por defecto 8080):");
                    puerto = Console.ReadLine();
                    puerto = puerto.Length == 0 ? "8080" : puerto;

                    Console.WriteLine("¿Cuantos clientes desea abrir por todos? (por defecto son 20):");
                    string strCantClientes = Console.ReadLine();
                    Console.WriteLine();

                    int cantClientes;
                    if (!int.TryParse(strCantClientes, out cantClientes))
                        cantClientes = 20;

                    // Abrir máximo 20 clientes por todos.
                    if (cantClientes > 20)
                        cantClientes = 20;

                    // Abrir los otros clientes.
                    for (var i = 1; i < cantClientes; i++)
                    {
                        Process.Start(@"GeneradorTexto.exe", string.Format("{0} {1}", servidor, puerto));
                    }
                }
                url = string.Format("{0}:{1}/", servidor, puerto);
            
                Console.WriteLine("Iniciando cliente...");

                CrearTextosAleatorios();
                InicializarTemporizador();
            
                Console.Clear();
                Console.WriteLine(@"Enviando textos aleatorios a la API {0}", url);
                Console.WriteLine();
                Console.WriteLine("Presione cualquier tecla para terminar la ejecución");
                Console.WriteLine();
                Console.ReadKey(false);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ocurrió cargando la aplicación: " + ex.Message);
                Console.ReadKey(false);
                Environment.Exit(0);
            }
        }

        private static void CrearTextosAleatorios()
        {
            // Requerimiento:
            // Los textos creados tienen espacios, mayúsculas y minúsculas, comas, 
            // puntos aparte (punto y salto de línea) y puntos seguidos, 
            // y deberán usar todas las letras del alfabeto español (pueden incluir la ñ y la Ñ), 
            // y los números del 0 al 9. 

            try
            {
                // Longitud mínima del texto.
                var textoMinimo = 1024;

                // Caracteres alfanuméricos que incluye el texto.
                var letras = "abcdefghijklmnñopqrstuvwxyzABCDEFGHIJKLMNÑOPQRSTUVWXYZ0123456789";

                // Espacio, coma, punto seguido y punto aparte que también se incluyen en el texto.
                // Para disminuir la cantidad de signos de puntuación en el texto (puntos y comas) 
                // entonces debemos agregar más elementos espacio al arreglo.
                var noLetras = new string[] { " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", ", ", ". ", "." + Environment.NewLine };

                int indice;
                int longitudPalabra;
                bool adicionarPalabra;

                genRandom = new Random();
                arregloTextos = new string[cantTextos];
                var textoAleatorio = new StringBuilder();

                // Se tendrán "CantTextos" textos aleatorios por cliente para enviar al servidor.
                for (var i = 0; i < cantTextos; i++)
                {
                    // Iniciamos el texto adicionando una palabra.
                    adicionarPalabra = true;
                    do
                    {
                        if (adicionarPalabra)
                        {
                            // Las palabras tendrán una longitud de 1 a 10 caracteres.
                            longitudPalabra = genRandom.Next(1, 11);
                            for (var j = 0; j < longitudPalabra; j++)
                            {
                                // Construir la palabra con caracteres alfanuméricos aleatorios.
                                indice = genRandom.Next(0, letras.Length);
                                textoAleatorio.Append(letras[indice]);
                            }
                        }
                        else
                        {
                            indice = genRandom.Next(0, noLetras.Length);
                            textoAleatorio.Append(noLetras[indice]);
                        }

                        // Alternar entre adicionar palabra y adicionar caracter NO alfanumérico.
                        adicionarPalabra = !adicionarPalabra;

                        // Se adicionan caracteres hasta que sobrepase la cantidad mínima.
                    } while (textoAleatorio.Length <= textoMinimo);

                    // Asignar el texto aleatorio recien creado dentro de una cadena JSON 
                    // bajo la llave "Texto" así:
                    // {
                    //     "texto": "texto de ejemplo."
                    // }

                    textoAleatorio.Insert(0, "{\"texto\":\"");
                    textoAleatorio.Append("\"}");

                    // Se adiciona un elemento al arreglo de textos aleatorios.
                    arregloTextos[i] = textoAleatorio.ToString();
                    textoAleatorio.Clear();
                }
            }
            catch(Exception ex)
            {
                close("Ocurrió un error creando los textos aleatorios: " + ex.Message);
            }
        }

        private static void InicializarTemporizador()
        {
            // Requerimiento:
            // Cada cliente deberá hacer como mínimo 1000 posts por segundo de textos de al menos 1 KByte cada uno.
            try
            {
                // Inicializar el temporizador a intervalos de 1 milisegundo.
                temporiza = new Timer(intervalo);

                // Definir el evento que se ejecutará cada milisegundo.
                temporiza.Elapsed += new ElapsedEventHandler(temporiza_Elapsed);

                // Encender el temporizador.
                temporiza.Enabled = true;
            }
            catch (Exception ex)
            {
                close("Ocurrió un error inicializando el temporizador: " + ex.Message);
            }
        }

        private static void temporiza_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Para enviar el texto se utiliza un hilo aparte para que no afecte
            // los intervalos de disparo del temporizador.
            Task t = Task.Factory.StartNew(() =>
            {
                try
                {
                    // Inicializar el objeto httpWebRequest que hará un POST http cada milisegundo a un API REST/json.
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                    httpWebRequest.ContentType = "application/json";
                    httpWebRequest.Method = "POST";
                    
                    // Enviar uno de los textos aleatorios creados al azar.
                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream(), codificacion))
                    {
                        // Colocar la información del texto en el buffer.
                        streamWriter.Write( arregloTextos[genRandom.Next(0, cantTextos)] );
                        streamWriter.Flush();
                    }

                    // Enviar la información del buffer al servidor de forma asincrona.
                    httpWebRequest.GetResponse();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ocurrió un error enviando el texto al servidor: " + ex.Message);
                }
            });
        }

        private static void close(string Error)
        {
            close();
            Console.WriteLine(Error);
            Console.ReadKey(false);
            Environment.Exit(0);
        }

        private static void close()
        {
            if (temporiza != null)
            {
                // Liberar los recursos.
                temporiza.Close();
                temporiza.Dispose();
            }
        }

    }
}

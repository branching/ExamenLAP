using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Web;
using Newtonsoft.Json;

namespace AnalizadorTexto
{
    class Program
    {
        // Nombre del archivo.
        private static string nombreArchivo = "log.txt";
        // Estandarizar la codificación de texto que se va a manejar. La codificación iso-8859-3 es para caracteres latinos.
        private static Encoding codificacion = Encoding.GetEncoding("iso-8859-3");
        private static HttpListener listener;
        private static Thread listenThread;
        private static FileStream fileStream;
        private static StreamWriter streamWriter;
        private static Mutex mutex;

        static void Main(string[] args)
        {
            try
            {
                // Se inicializa la dirección URL.
                Console.WriteLine(@"Ingrese la URL donde quedará disponible la API (por defecto http://localhost):");
                string servidor = Console.ReadLine();
                servidor = servidor.Length == 0 ? @"http://localhost" : servidor;

                Console.WriteLine("Ingrese el puerto (por defecto 8080):");
                string puerto = Console.ReadLine();
                puerto = puerto.Length == 0 ? "8080" : puerto;

                var url = string.Format("{0}:{1}/", servidor, puerto);

                // Se abre el archivo y se deja abierto durante todo 
                // el funcionamiento de la aplicación para lograr mayor performance.
                fileStream = new FileStream(nombreArchivo, FileMode.OpenOrCreate,
                         FileAccess.Write, FileShare.Read, 65536, FileOptions.None);

                // El texto se escribe en el archivo en codificación caracteres latinos.
                streamWriter = new StreamWriter(fileStream, codificacion);

                mutex = new Mutex();

                // Comenzar a escuchar las peticiones http.
                listener = new HttpListener();
                listener.Prefixes.Add(url);
                listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                listener.Start();

                // Recibir las peticiones http en un hilo de ejecución diferente al principal.
                listenThread = new Thread(new ParameterizedThreadStart(Startlistener));
                listenThread.Start();

                Console.Clear();
                Console.WriteLine(@"API inicializada en la dirección {0}", url);
                Console.WriteLine();
                Console.WriteLine("Presione cualquier tecla para terminar la ejecución");
                Console.WriteLine();
                Console.ReadKey(false);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ocurrió un error cargando la aplicación: " + ex.Message);
                Console.ReadKey(false);
                Environment.Exit(0);
            }
        }

        private static void Startlistener(object s)
        {
            while (true)
            {
                // Bloquear hasta que un cliente se conecte al servidor.
                ProcessRequest();
            }
        }

        private static void ProcessRequest()
        {
            var result = listener.BeginGetContext(ListenerCallback, listener);
            result.AsyncWaitHandle.WaitOne();
        }

        private static void ListenerCallback(IAsyncResult result)
        {
            // Procesar cada petición http en un hilo aparte.
            Task t = Task.Factory.StartNew(() =>
            {
                string json;

                try
                {
                    HttpListenerContext context = listener.EndGetContext(result);

                    // Con la siguiente línea se procesan unicamente las peticiones POST.
                    json = new StreamReader(context.Request.InputStream, codificacion).ReadToEnd();

                    // Responder al cliente de la manera más corta y cuanto antes.
                    context.Response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ocurrió un error obteniendo el Request: " + ex.Message);
                    return;
                }

                AnalizarEscribir(json);
            });
        }

        private static void AnalizarEscribir(string json)
        {
            // Requerimiento:
            // Los textos creados tienen espacios, mayúsculas y minúsculas, comas, 
            // puntos aparte (punto y salto de línea) y puntos seguidos, 
            // y deberán usar todas las letras del alfabeto español (pueden incluir la ñ y la Ñ), 
            // y los números del 0 al 9. 
                 
            // Cada texto recibido por el backend a través del API REST debe ser analizado para sacar las siguientes estadísticas: 
            // 1) contar las palabras de dicho archivo que terminen con la letra n.
            // 2) contar el número de oraciones que contengan más de 15 palabras. 
            //     Entiéndase por oración aquel conjunto de palabras que terminan en un punto SEGUIDO.
            // 3) contar el número de párrafos. Entiéndase por párrafo aquel conjunto de palabras que terminan en un punto APARTE.
            // 4) contar el número de caracteres alfanuméricos distintos a n o N que contenga el archivo.
            //     Entiéndase por caracteres alfanuméricos toda letra que pertenezca a los rangos [A Z], [a z] y [0 9].
            //     Es decir ni el punto ni la coma, ni el espacio, (ni el salto de línea) 
            //     son considerados caracteres alfanuméricos.

            StringBuilder textoEscribir;
            try
            {
                var esSepararadorPalabras = false;
                var esPuntoSeguido = false;
                var esPuntoAparte = false;
                var caracterAnteriorEs_n = false;
                var caracterAnteriorEsAlfanum = false;
                var caracterAnteriorEsPunto = false;
                var numPalabrasTerminadasEn_n = 0;
                var numPalabrasEnOracion = 0;
                var numOracionesMayor15 = 0;
                var numParrafos = 0;
                var numAlfanumDiferentes_nN = 0;

                string texto = JsonConvert.DeserializeObject<ClsJson>(json).texto;

                for (var i = 0; i < texto.Length; i++)
                {
                    switch (texto[i])
                    {
                        case ' ':
                            esSepararadorPalabras = true;
                            // Uno o varios espacios después de un punto NO definen si es punto seguido o punto aparte.
                            break;
                        case ',':
                            esSepararadorPalabras = true;
                            // Si después del punto hay una coma, se cuenta como punto seguido.
                            if (caracterAnteriorEsPunto)
                                esPuntoSeguido = true;
                            break;
                        case '.':
                            esSepararadorPalabras = true;
                            caracterAnteriorEsPunto = true;
                            break;
                        case '\r':
                            continue;
                        case '\n':
                            esSepararadorPalabras = true;
                            // El salto de linea identifica el punto aparte.
                            if (caracterAnteriorEsPunto)
                                esPuntoAparte = true;
                            break;
                        case 'n':
                            // Contar las palabras de dicho texto que terminen con la letra n.
                            caracterAnteriorEs_n = true;
                            caracterAnteriorEsAlfanum = true;
                            if (caracterAnteriorEsPunto)
                                esPuntoSeguido = true;
                            break;
                        case 'N':
                            caracterAnteriorEs_n = false;
                            caracterAnteriorEsAlfanum = true;
                            if (caracterAnteriorEsPunto)
                                esPuntoSeguido = true;
                            break;
                        default:
                            caracterAnteriorEs_n = false;
                            caracterAnteriorEsAlfanum = true;
                            if (caracterAnteriorEsPunto)
                                esPuntoSeguido = true;
                            numAlfanumDiferentes_nN++;
                            break;
                    }

                    // Las palabras vienen separadas por espacio, coma, punto o salto de linea.
                    if (esSepararadorPalabras)
                    {
                        // Si el caracter anterior es alfanumércio, entonces es el final de una palabra
                        if (caracterAnteriorEsAlfanum)
                        {
                            numPalabrasEnOracion++;
                            caracterAnteriorEsAlfanum = false;

                            // Validar si el caracter alfanumérico es una n.
                            if (caracterAnteriorEs_n)
                            {
                                numPalabrasTerminadasEn_n++;
                                caracterAnteriorEs_n = false;
                            }
                        }
                        esSepararadorPalabras = false;
                    }

                    // El punto seguido y el punto aparte son excluyentes.
                    if (esPuntoSeguido)
                    {
                        // Entiéndase por oración aquel conjunto de palabras que terminan en un punto SEGUIDO.
                        if (numPalabrasEnOracion > 15)
                            numOracionesMayor15++;
                        numPalabrasEnOracion = 0;
                        caracterAnteriorEsPunto = false;
                        esPuntoSeguido = false;
                    }
                    else if (esPuntoAparte)
                    {
                        // Entiéndase por párrafo aquel conjunto de palabras que terminan en un punto APARTE.
                        numParrafos++;
                        caracterAnteriorEsPunto = false;
                        esPuntoAparte = false;
                    }
                }

                // Si la última palabra del texto termina en n entonces se incrementa el contador.
                if (caracterAnteriorEs_n)
                    numPalabrasTerminadasEn_n++;

                // Si el texto finaliza con un punto, se toma como punto aparte,
                // es decir, que se cuenta un párrafo más. Pero NO se cuenta una oración más.
                if (caracterAnteriorEsPunto)
                    numParrafos++;

                textoEscribir = new StringBuilder();

                // Construir todo el texto que se va a guardar en disco duro.
                textoEscribir.AppendLine("\tTEXTO ANALIZADO");
                textoEscribir.AppendLine(texto).AppendLine();
                textoEscribir.AppendLine("\tESTADISTICAS");
                textoEscribir.Append("\t\tPalabras terminadas en n = ").Append(numPalabrasTerminadasEn_n).AppendLine();
                textoEscribir.Append("\t\tOraciones con más de 15 palabras = ").Append(numOracionesMayor15).AppendLine();
                textoEscribir.Append("\t\tPárrafos = ").Append(numParrafos).AppendLine();
                textoEscribir.Append("\t\tCaracteres alfanuméricos distintos a n o N = ").Append(numAlfanumDiferentes_nN).AppendLine().AppendLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ocurrió un error analizando el texto aleatorio: " + ex.Message);
                return;
            }

            // Escribir en el archivo: con mutex nos aseguramos que 
            // al código de escritura sólo accede un hilo a la vez.
            mutex.WaitOne();
            try
            {
                streamWriter.Write(textoEscribir.ToString());
                streamWriter.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ocurrió un error escribiendo el archivo: " + ex.Message);
            }
            finally
            {
                mutex.ReleaseMutex();
            }

        }
    }
}

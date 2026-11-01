
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VeterinariaServerSurPia.Services
{
    public sealed class OllamaService
    {
        private const string UrlOllama =
            "http://127.0.0.1:11434/api/chat";

        private const string NombreModelo = "qwen3:4b";

        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private const string InstruccionesSistema =
            "Eres VetSmart AI 2.0, un asistente de apoyo para una clínica veterinaria. " +
            "Responde siempre en español claro, natural y profesional. " +
            "Responde directamente a la pregunta, sin mostrar razonamientos internos, " +
            "pensamientos, borradores ni instrucciones sobre cómo elaboras la respuesta. " +
            "Los nombres mencionados en la información proporcionada corresponden a " +
            "pacientes de la clínica. No los confundas con personajes ficticios ni personas. " +
            "Utiliza los datos de la mascota únicamente como información proporcionada " +
            "por el sistema. No inventes antecedentes, vacunas, resultados, diagnósticos " +
            "ni tratamientos. Si un dato no está disponible, dilo expresamente. " +
            "Distingue entre información registrada, orientación general y datos faltantes. " +
            "No afirmes que has consultado expedientes o bases de datos que no te fueron " +
            "proporcionados. No sustituyas al médico veterinario ni presentes una sospecha " +
            "como diagnóstico confirmado. Si describes signos de urgencia, recomienda " +
            "valoración veterinaria inmediata. " +
            "Responde con la extensión necesaria según la pregunta. " +
            "Para preguntas sencillas, responde brevemente. " +
            "Para consultas de historiales médicos, proporciona los detalles relevantes disponibles.";

        public async Task<string> GenerarRespuestaAsync(
            string contextoMascota,
            string pregunta)
        {
            if (string.IsNullOrWhiteSpace(pregunta))
            {
                throw new ArgumentException(
                    "La pregunta no puede estar vacía.",
                    nameof(pregunta));
            }

            string contexto = string.IsNullOrWhiteSpace(contextoMascota)
                ? "No hay información disponible."
                : contextoMascota;

            string mensajeUsuario =
                "INFORMACIÓN DISPONIBLE DE LA APLICACIÓN:\n" +
                contexto +
                "\n\nPREGUNTA DEL USUARIO:\n" +
                pregunta +
                "\n\nResponde directamente en español. " +
                "No incluyas pensamientos ni razonamientos internos.";

            string texto = await ConsultarOllamaAsync(
                InstruccionesSistema,
                mensajeUsuario);

            texto = LimpiarRespuesta(texto);

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new InvalidOperationException(
                    "Ollama no generó una respuesta final válida.");
            }

            return texto;
        }

        private async Task<string> ConsultarOllamaAsync(
            string instrucciones,
            string mensajeUsuario)
        {
            var solicitud = new
            {
                model = NombreModelo,
                stream = false,
                think = "low",
                keep_alive = "10m",

                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = instrucciones
                    },
                    new
                    {
                        role = "user",
                        content = mensajeUsuario
                    }
                },

                options = new
                {
                    temperature = 0.3,
                    num_ctx = 8192,
                    num_predict = 4096
                }
            };

            string json = JsonSerializer.Serialize(solicitud);

            using var contenido = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage respuesta =
                await httpClient.PostAsync(
                    UrlOllama,
                    contenido);

            string cuerpo =
                await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Ollama respondió con HTTP {(int)respuesta.StatusCode}: {cuerpo}");
            }

            using JsonDocument documento =
                JsonDocument.Parse(cuerpo);

            JsonElement raiz = documento.RootElement;

            if (!raiz.TryGetProperty(
                    "message",
                    out JsonElement mensaje))
            {
                throw new InvalidOperationException(
                    "Ollama respondió, pero no se encontró el objeto message.");
            }

            string texto = string.Empty;
            string razonamiento = string.Empty;
            string motivoFinalizacion = "No disponible";
            string tokensGenerados = "No disponible";
            string tokensEntrada = "No disponible";

            if (mensaje.TryGetProperty(
                    "content",
                    out JsonElement contenidoRespuesta))
            {
                texto = contenidoRespuesta.GetString() ?? string.Empty;
            }

            if (mensaje.TryGetProperty(
                    "thinking",
                    out JsonElement pensamiento))
            {
                razonamiento = pensamiento.GetString() ?? string.Empty;
            }

            if (raiz.TryGetProperty(
                    "done_reason",
                    out JsonElement motivo))
            {
                motivoFinalizacion = motivo.ToString();
            }

            if (raiz.TryGetProperty(
                    "eval_count",
                    out JsonElement evaluacion))
            {
                tokensGenerados = evaluacion.ToString();
            }

            if (raiz.TryGetProperty(
                    "prompt_eval_count",
                    out JsonElement evaluacionPrompt))
            {
                tokensEntrada = evaluacionPrompt.ToString();
            }

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new InvalidOperationException(
                    "Ollama devolvió una respuesta vacía.\n\n" +
                    "DIAGNÓSTICO:\n" +
                    $"Modelo: {NombreModelo}\n" +
                    $"Motivo de finalización: {motivoFinalizacion}\n" +
                    $"Tokens de entrada: {tokensEntrada}\n" +
                    $"Tokens generados: {tokensGenerados}\n" +
                    $"Caracteres de razonamiento: {razonamiento.Length}\n" +
                    $"Caracteres de respuesta final: {texto.Length}\n" +
                    $"Caracteres del mensaje enviado: {mensajeUsuario.Length}\n\n" +
                    "No se muestra el razonamiento interno del modelo.");
            }

            return texto.Trim();
        }

        private static string LimpiarRespuesta(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            texto = texto.Trim();

            texto = Regex.Replace(
                texto,
                @"(?is)<think\b[^>]*>.*?</think>",
                string.Empty);

            if (Regex.IsMatch(
                    texto,
                    @"(?is)<think\b"))
            {
                return string.Empty;
            }

            texto = Regex.Replace(
                texto,
                @"(?is)</?think\b[^>]*>",
                string.Empty);

            return texto.Trim();
        }
    }
}

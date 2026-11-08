
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VeterinariaServerSurPia.Services
{
    public sealed class OllamaService
    {
        private const string UrlOllama =
            "http://127.0.0.1:11434/api/chat";

        private const string UrlOllamaGenerar =
            "http://127.0.0.1:11434/api/generate";

        private const string NombreModelo = "qwen2.5:3b";

        // Límites para que las respuestas sean rápidas y enfocadas.
        private const int MaxCaracteresContexto = 6000;
        private const int MaxTokensConsultaSimple = 220;
        private const int MaxTokensConsultaClinica = 450;
        private const int ContextoModelo = 4096;

        private const string MensajeFueraDeDominio =
            "Soy VetSmart AI 2.0, un asistente especializado exclusivamente " +
            "en medicina veterinaria y en la gestión de una clínica veterinaria. " +
            "No puedo atender consultas ajenas a estas áreas. " +
            "Puedo ayudarte con casos clínicos, pacientes, expedientes, " +
            "diagnósticos, tratamientos, citas, cartillas médicas y otros " +
            "asuntos relacionados con el trabajo veterinario.";

        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        // Prompt de sistema corto: menos texto = respuestas más rápidas
        // y un modelo pequeño que se desvía menos de la pregunta.
        private const string InstruccionesSistema =
            "Eres VetSmart AI, asistente de apoyo clínico y administrativo " +
            "para médicos veterinarios y personal de una clínica veterinaria. " +
            "Hablas de profesional a profesional: nunca le indiques al usuario " +
            "que lleve al paciente con un veterinario. " +

            "REGLAS: " +
            "1) Responde SOLO lo que se pregunta, en español y de forma directa. " +
            "No agregues secciones, recomendaciones ni datos que no pidieron. " +
            "2) Datos puntuales: una o dos oraciones. " +
            "Casos clínicos: hasta 4 diagnósticos diferenciales ordenados por " +
            "probabilidad, cada uno con una línea de justificación basada en " +
            "los hallazgos, y la prueba clave que lo confirma o descarta. " +
            "Señala en una línea si hay signos de urgencia. " +
            "3) Usa únicamente los datos de la aplicación. Si algo no aparece, " +
            "responde que no está registrado. No inventes datos, citas, dosis, " +
            "referencias ni resultados. Cuando resumas observaciones o " +
            "antecedentes, repite solo lo que está escrito: no agregues " +
            "síntomas, medicación, causas ni fechas que no aparezcan " +
            "textualmente. " +
            "4) Nunca copies, repitas ni resumas el bloque de datos recibido. " +
            "5) Ausencia de registro no significa ausencia de enfermedad, " +
            "alergia, vacuna o tratamiento. " +
            "6) Atiendes solo medicina veterinaria y gestión de la clínica. " +
            "7) Una cita registrada no es un horario disponible; no confirmes " +
            "reservaciones. " +
            "8) Los casos de otros pacientes son solo referencia orientativa. " +
            "9) No muestres razonamientos internos ni uses formato Markdown.";

        public async Task<string> GenerarRespuestaAsync(
            string contextoMascota,
            string pregunta,
            Action<string>? alProgreso = null)
        {
            if (string.IsNullOrWhiteSpace(pregunta))
            {
                throw new ArgumentException(
                    "La pregunta no puede estar vacía.",
                    nameof(pregunta));
            }

            string contexto = string.IsNullOrWhiteSpace(contextoMascota)
                ? "No hay información disponible de la aplicación."
                : contextoMascota;

            if (!EsConsultaPermitida(pregunta, contexto))
            {
                return MensajeFueraDeDominio;
            }

            // Un contexto demasiado largo vuelve lento al modelo local.
            if (contexto.Length > MaxCaracteresContexto)
            {
                contexto =
                    contexto.Substring(0, MaxCaracteresContexto) +
                    "\n[Se omitieron registros adicionales para mantener " +
                    "la respuesta ágil.]";
            }

            string reglasEspecificas =
                ObtenerReglasClinicasEspecificas(
                    pregunta + "\n" + contexto);

            bool esClinica =
                !string.IsNullOrWhiteSpace(reglasEspecificas) ||
                EsPreguntaClinica(pregunta);

            string limite = esClinica
                ? "Máximo 150 palabras."
                : "Máximo 3 oraciones.";

            string mensajeUsuario =
                "DATOS DE LA APLICACIÓN (solo para consulta, no los repitas):\n" +
                contexto +
                "\n\nPREGUNTA DEL PROFESIONAL VETERINARIO:\n" +
                pregunta +
                "\n\nResponde únicamente a esa pregunta, de forma breve y " +
                "directa. Usa los datos anteriores solo cuando se necesiten " +
                "y no menciones los que no se preguntaron. " +
                limite;

            string instruccionesFinales = InstruccionesSistema;

            if (!string.IsNullOrWhiteSpace(reglasEspecificas))
            {
                instruccionesFinales +=
                    "\n\nREGLAS CLÍNICAS ESPECÍFICAS PARA ESTE CASO:\n" +
                    reglasEspecificas;
            }

            string texto = await ConsultarOllamaAsync(
                instruccionesFinales,
                mensajeUsuario,
                esClinica
                    ? MaxTokensConsultaClinica
                    : MaxTokensConsultaSimple,
                alProgreso);

            texto = LimpiarRespuesta(texto);

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new InvalidOperationException(
                    "Ollama no generó una respuesta final válida.");
            }

            return texto;
        }

        // Carga el modelo en memoria en segundo plano para que la primera
        // consulta no tenga que esperar a que Ollama lo cargue.
        public async Task PrecalentarModeloAsync()
        {
            try
            {
                var solicitud = new
                {
                    model = NombreModelo,
                    keep_alive = "30m"
                };

                using var contenido = new StringContent(
                    JsonSerializer.Serialize(solicitud),
                    Encoding.UTF8,
                    "application/json");

                using var limite = new CancellationTokenSource(
                    TimeSpan.FromMinutes(2));

                using HttpResponseMessage respuesta =
                    await httpClient.PostAsync(
                        UrlOllamaGenerar,
                        contenido,
                        limite.Token);
            }
            catch
            {
                // Si Ollama no está disponible todavía, no pasa nada:
                // el error se mostrará al hacer la consulta.
            }
        }

        private static bool EsPreguntaClinica(string pregunta)
        {
            string texto = NormalizarTexto(pregunta);

            return ContieneAlgunaFrase(
                texto,
                new[]
                {
                    "sintoma", "diagnostic", "tratamiento", "enfermedad",
                    "que tiene", "que puede tener", "que podria tener",
                    "por que", "que hacer", "que hago", "recomiend",
                    "dosis", "medicamento", "vomit", "diarrea", "fiebre",
                    "antecedente", "le pasa", "padece",
                    "dolor", "urgencia", "convulsion", "infeccion",
                    "cirugia", "analisis", "estudio", "pronostico"
                });
        }

        private static string ObtenerReglasClinicasEspecificas(
            string textoOriginal)
        {
            string texto = NormalizarTexto(textoOriginal);

            StringBuilder reglas = new StringBuilder();

            bool esCachorro =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "cachorro",
                        "cachorra",
                        "perro",
                        "perra",
                        "canino",
                        "canina"
                    });

            bool tieneDiarreaHemorragica =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "diarrea hemorragica",
                        "diarrea con sangre",
                        "heces con sangre",
                        "sangre en las heces",
                        "evacuaciones sanguinolentas"
                    });

            bool tieneVomitos =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "vomito",
                        "vomitos",
                        "emesis"
                    });

            bool tieneLeucopenia =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "leucopenia",
                        "leucocitos bajos",
                        "disminucion de leucocitos"
                    });

            bool vacunacionIncompleta =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "vacunacion incompleta",
                        "vacunacion parcial",
                        "esquema de vacunacion incompleto",
                        "no tiene todas sus vacunas",
                        "no esta vacunado",
                        "no esta completamente vacunado"
                    });

            if (esCachorro &&
                tieneDiarreaHemorragica &&
                tieneVomitos &&
                (tieneLeucopenia || vacunacionIncompleta))
            {
                reglas.Append(
                    "PATRÓN COMPATIBLE CON PARVOVIROSIS CANINA: " +
                    "El caso contiene edad juvenil o referencia a cachorro, " +
                    "signos gastrointestinales hemorrágicos y vómitos, " +
                    "además de leucopenia o vacunación incompleta. " +
                    "Debes colocar parvovirosis canina entre las primeras " +
                    "sospechas y normalmente como el diferencial prioritario. " +
                    "Explica la compatibilidad sin afirmar que está confirmada. " +
                    "No sustituyas esta sospecha por sepsis o infección " +
                    "bacteriana sin evidencia adicional. " +
                    "Considera antígeno fecal para parvovirus y PCR cuando " +
                    "esté indicada. Complementa con hemograma, hematocrito, " +
                    "proteínas totales, glucosa y electrolitos. " +
                    "Evalúa perfusión, hidratación y riesgo de hipovolemia. " +
                    "No propongas cultivos de orina o sangre como pruebas " +
                    "rutinarias para confirmar parvovirosis. " +
                    "Los diferenciales adicionales deben estar justificados " +
                    "y no desplazar injustificadamente esta sospecha. ");
            }

            bool gato =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "gato",
                        "gata",
                        "felino",
                        "felina"
                    });

            bool dificultadRespiratoria =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "dificultad respiratoria",
                        "disnea",
                        "respira con la boca abierta",
                        "respiracion con la boca abierta",
                        "mucosas azuladas",
                        "cianosis"
                    });

            if (gato && dificultadRespiratoria)
            {
                reglas.Append(
                    "\nPATRÓN DE URGENCIA RESPIRATORIA FELINA: " +
                    "Prioriza minimizar el estrés, oxigenoterapia y " +
                    "estabilización. La cianosis es compatible con " +
                    "oxigenación deficiente y no equivale a anemia. " +
                    "Evita procedimientos estresantes antes de estabilizar " +
                    "al paciente. No coloques cultivo de orina como prueba " +
                    "para identificar parásitos respiratorios. ");
            }

            bool azotemia =
                ContieneAlgunaFrase(
                    texto,
                    new[]
                    {
                        "creatinina elevada",
                        "creatinina alta",
                        "urea elevada",
                        "urea alta",
                        "azotemia",
                        "bun elevado"
                    });

            if (azotemia)
            {
                reglas.Append(
                    "\nPATRÓN DE ALTERACIÓN RENAL: " +
                    "Distingue azotemia prerrenal, renal y posrenal. " +
                    "No confirmes enfermedad renal crónica únicamente " +
                    "por urea y creatinina elevadas. Correlaciona con " +
                    "hidratación, densidad urinaria, urianálisis, " +
                    "evolución temporal, presión arterial y otros hallazgos. ");
            }

            return reglas.ToString();
        }

        private static bool EsConsultaPermitida(
            string pregunta,
            string contexto)
        {
            string preguntaNormalizada = NormalizarTexto(pregunta);
            string contextoNormalizado = NormalizarTexto(contexto);

            bool contieneTemaVeterinario =
                ContieneTemaVeterinario(preguntaNormalizada);

            bool contieneTemaAjeno =
                ContieneTemaAjenoExplicito(preguntaNormalizada);

            if (contieneTemaAjeno && !contieneTemaVeterinario)
            {
                return false;
            }

            if (contieneTemaVeterinario)
            {
                return true;
            }

            // Si la pregunta menciona por nombre (o ID) a la mascota
            // seleccionada, es una consulta sobre un paciente.
            if (MencionaMascotaDelContexto(preguntaNormalizada, contexto))
            {
                return true;
            }

            if (ContieneTemaVeterinario(contextoNormalizado) &&
                EsPreguntaDeSeguimiento(preguntaNormalizada))
            {
                return true;
            }

            return false;
        }

        // Busca en el contexto el nombre y el ID de la mascota seleccionada
        // ("Nombre: Max", "ID: M0004") y revisa si la pregunta los menciona.
        private static bool MencionaMascotaDelContexto(
            string preguntaNormalizada,
            string contexto)
        {
            if (string.IsNullOrWhiteSpace(contexto))
            {
                return false;
            }

            var referencias = new List<string>();

            Match id = Regex.Match(
                contexto,
                @"(?im)^\s*ID:\s*(\S+)\s*$");

            if (id.Success)
            {
                referencias.Add(NormalizarTexto(id.Groups[1].Value));
            }

            Match nombre = Regex.Match(
                contexto,
                @"(?im)^\s*Nombre:\s*(.+?)\s*$");

            if (nombre.Success)
            {
                string completo = NormalizarTexto(nombre.Groups[1].Value);

                referencias.Add(completo);

                // Nombres compuestos ("Max Junior"): basta con la primera palabra.
                string primera = completo
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "";

                if (primera.Length >= 3)
                {
                    referencias.Add(primera);
                }
            }

            referencias = referencias
                .Where(r => r.Length >= 2)
                .Distinct()
                .ToList();

            return referencias.Count > 0 &&
                   ContieneAlgunaFrase(
                       preguntaNormalizada,
                       referencias.ToArray());
        }

        private static bool ContieneTemaVeterinario(string texto)
        {
            string[] palabrasPermitidas =
            {
                "veterinaria", "veterinario", "veterinarios",
                "mascota", "mascotas", "animal", "animales",
                "perro", "perros", "perrita", "perrito",
                "gato", "gatos", "gatito", "gatitos",
                "canino", "canina", "felino", "felina",
                "cachorro", "cachorros", "ave", "aves",
                "conejo", "conejos", "roedor", "roedores",
                "reptil", "reptiles", "equino", "bovino",
                "porcino", "ovino", "caprino", "hamster",
                "huron", "hurones",

                "antecedente", "antecedentes", "padece",
                "enfermo", "enferma", "historia clinica",
                "sintoma", "sintomas", "signo clinico",
                "signos clinicos", "diagnostico", "diagnosticos",
                "diagnosticar", "enfermedad", "enfermedades",
                "patologia", "patologias", "infeccion",
                "infecciones", "inflamacion", "inflamatorio",
                "fiebre", "vomito", "vomitos", "diarrea",
                "tos", "estornudo", "estornudos",
                "convulsion", "convulsiones", "letargo",
                "deshidratacion", "dolor", "cojera",
                "herida", "heridas", "sangrado", "hemorragia",
                "alergia", "alergias", "intoxicacion",
                "intoxicaciones", "paralisis", "tumor", "tumores",
                "neoplasia", "neoplasias", "anemia",
                "insuficiencia renal", "insuficiencia cardiaca",
                "perdida de apetito", "falta de apetito",
                "anorexia", "polidipsia", "poliuria",
                "prurito", "ictericia", "ascitis",
                "edema", "disnea", "taquicardia",
                "bradicardia", "hipotermia", "hipertermia",
                "leucopenia", "cianosis", "azotemia",

                "parvovirus", "moquillo", "distemper",
                "ehrlichiosis", "anaplasmosis", "babesiosis",
                "leptospirosis", "giardiasis", "toxoplasmosis",
                "pancreatitis", "piometra", "piometritis",
                "leishmaniasis", "rabia", "calicivirus",
                "rinotraqueitis", "leucemia felina",
                "inmunodeficiencia felina", "peritonitis infecciosa felina",
                "dermatitis", "otitis", "conjuntivitis",
                "gastritis", "enteritis", "colitis",
                "hepatitis", "nefritis", "cistitis",
                "urolitiasis", "diabetes mellitus",
                "hipotiroidismo", "hipertiroidismo",
                "displasia de cadera", "sarna", "tina",
                "pulgas", "garrapatas", "gusanos", "parasitos",

                "medicina interna", "farmacologia veterinaria",
                "cirugia veterinaria", "anestesia",
                "cardiologia", "dermatologia", "neurologia",
                "oftalmologia", "oncologia", "ortopedia",
                "gastroenterologia", "nefrologia",
                "endocrinologia", "reproduccion animal",
                "nutricion animal", "etologia",
                "comportamiento animal", "zoonosis",
                "parasitologia", "microbiologia",
                "urgencia veterinaria", "urgencias veterinarias",
                "medicina preventiva", "medicina felina",
                "medicina canina", "medicina de animales exoticos",

                "tratamiento", "tratamientos",
                "medicamento", "medicamentos",
                "farmaco", "farmacos", "dosis",
                "antibiotico", "antibioticos",
                "analgesico", "analgesicos",
                "antiinflamatorio", "antiinflamatorios",
                "vacuna", "vacunas", "vacunacion",
                "desparasitacion", "desparasitante",
                "esterilizacion", "castracion",
                "radiografia", "ultrasonido",
                "ecografia", "laboratorio",
                "hemograma", "biometria hematica",
                "quimica sanguinea", "coproparasitoscopico",
                "uroanalisis", "biopsia", "citologia",
                "prueba diagnostica", "pruebas diagnosticas",
                "pronostico", "pronosticos",
                "exploracion fisica", "examen fisico",
                "temperatura corporal", "frecuencia cardiaca",
                "frecuencia respiratoria", "presion arterial",
                "creatinina", "urea", "bun", "alt", "ast",
                "fosfatasa alcalina", "glucosa", "albumina",
                "globulinas", "hematocrito", "plaquetas",
                "leucocitos", "eritrocitos", "reticulocitos",
                "densidad urinaria", "sedimento urinario",
                "electrolitos", "lactato",

                "especie", "raza", "peso", "edad",
                "sexo", "esterilizado", "gestacion",
                "embarazo animal", "lactancia",
                "alimentacion animal", "dieta veterinaria",

                "clinica veterinaria", "hospital veterinario",
                "consultorio veterinario", "paciente",
                "pacientes", "expediente", "expedientes",
                "historial medico", "historial clinico",
                "cartilla medica", "cartilla de vacunacion",
                "cita", "citas", "agenda",
                "horario disponible", "horarios disponibles",
                "horarios libres", "disponibilidad",
                "consulta veterinaria", "consultas veterinarias",
                "propietario", "propietarios",
                "dueno de la mascota", "duenos de mascotas",
                "cliente", "clientes",
                "registro de mascota", "registro de mascotas",
                "servicio veterinario", "servicios veterinarios",
                "inventario veterinario", "farmacia veterinaria",
                "recepcion veterinaria",
                "venta de medicamentos veterinarios",
                "consulta de hoy", "citas de hoy",
                "agenda de hoy", "agenda veterinaria"
            };

            return ContieneAlgunaFrase(texto, palabrasPermitidas);
        }

        private static bool ContieneTemaAjenoExplicito(string texto)
        {
            string[] temasAjenoExplicitos =
            {
                "resuelve esta ecuacion",
                "resuelve la ecuacion",
                "hazme una tarea de matematicas",
                "tarea de matematicas",
                "calcula esta integral",
                "calcula esta derivada",
                "resuelve este problema matematico",
                "explicame algebra",
                "explicame trigonometria",

                "escribe codigo en",
                "programa en python",
                "programa en java",
                "programa en c#",
                "programa en javascript",
                "crea una pagina web",
                "desarrolla una aplicacion",
                "depura este codigo",
                "corrige este codigo",
                "error de compilacion",
                "error de programacion",
                "como instalar windows",
                "configurar mi computadora",

                "historia de mexico",
                "historia universal",
                "segunda guerra mundial",
                "primera guerra mundial",
                "presidente de mexico",
                "elecciones presidenciales",
                "capital de francia",
                "capital de mexico",
                "guerra mundial",

                "resultado del partido",
                "quien gano el partido",
                "partido de futbol",
                "partidos de futbol",
                "mundial de futbol",
                "videojuego",
                "videojuegos",
                "pelicula",
                "peliculas",
                "serie de television",
                "series de television",
                "recomiendame musica",
                "letra de una cancion"
            };

            return ContieneAlgunaFrase(texto, temasAjenoExplicitos);
        }

        private static bool ContieneAlgunaFrase(
            string texto,
            string[] frases)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return false;
            }

            return frases.Any(frase =>
            {
                string patron =
                    @"(?<![\p{L}\p{N}])" +
                    Regex.Escape(frase).Replace(" ", @"\s+") +
                    @"(?![\p{L}\p{N}])";

                return Regex.IsMatch(
                    texto,
                    patron,
                    RegexOptions.IgnoreCase);
            });
        }

        private static bool EsPreguntaDeSeguimiento(string pregunta)
        {
            if (string.IsNullOrWhiteSpace(pregunta))
            {
                return false;
            }

            string[] indicadoresSeguimiento =
            {
                "y entonces",
                "y que sigue",
                "que sigue",
                "y despues",
                "que tratamiento",
                "cual tratamiento",
                "que dosis",
                "cual dosis",
                "que medicamento",
                "cual medicamento",
                "que significa",
                "explicame eso",
                "puedes explicarlo",
                "amplia la informacion",
                "mas informacion",
                "resumelo",
                "resume lo anterior",
                "y en ese caso",
                "que recomiendas",
                "cual seria el siguiente paso",
                "cuales son los riesgos",
                "que otras posibilidades",
                "y si presenta",
                "y si tiene",
                "que tiene",
                "que podria tener",
                "es grave",
                "puede ser",
                "cual seria el diagnostico",
                "que pruebas recomiendas",
                "que estudios solicito",
                "que hago despues",
                "y para ese paciente",
                "y para esta mascota",
                "sobre ese paciente",
                "sobre ese caso",
                "en ese paciente",
                "en ese caso",
                "y con esos resultados",
                "interpreta esos resultados",
                "que significa ese resultado",
                "cual es el pronostico"
            };

            return pregunta.Length <= 350 &&
                   ContieneAlgunaFrase(
                       pregunta,
                       indicadoresSeguimiento);
        }

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            string normalizado = texto.ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new StringBuilder();

            foreach (char caracter in normalizado)
            {
                UnicodeCategory categoria =
                    CharUnicodeInfo.GetUnicodeCategory(caracter);

                if (categoria != UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(caracter);
                }
            }

            return resultado.ToString()
                .Normalize(NormalizationForm.FormC);
        }

        private async Task<string> ConsultarOllamaAsync(
            string instrucciones,
            string mensajeUsuario,
            int maxTokens,
            Action<string>? alProgreso)
        {
            var solicitud = new
            {
                model = NombreModelo,

                // La respuesta llega por partes y se muestra mientras
                // se genera, así el usuario no espera a que termine.
                stream = true,
                keep_alive = "30m",

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
                    temperature = 0.2,
                    top_p = 0.9,
                    repeat_penalty = 1.1,
                    num_ctx = ContextoModelo,
                    num_predict = maxTokens
                }
            };

            string json = JsonSerializer.Serialize(solicitud);

            using var contenido = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using var peticion = new HttpRequestMessage(
                HttpMethod.Post,
                UrlOllama)
            {
                Content = contenido
            };

            using HttpResponseMessage respuesta =
                await httpClient.SendAsync(
                    peticion,
                    HttpCompletionOption.ResponseHeadersRead);

            if (!respuesta.IsSuccessStatusCode)
            {
                string cuerpo =
                    await respuesta.Content.ReadAsStringAsync();

                throw new InvalidOperationException(
                    $"Ollama respondió con HTTP " +
                    $"{(int)respuesta.StatusCode}: {cuerpo}");
            }

            using Stream flujo =
                await respuesta.Content.ReadAsStreamAsync();

            using var lector = new StreamReader(flujo, Encoding.UTF8);

            var acumulado = new StringBuilder();
            string motivo = "No disponible";
            string? linea;

            while ((linea = await lector.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(linea))
                    continue;

                using JsonDocument documento = JsonDocument.Parse(linea);
                JsonElement raiz = documento.RootElement;

                if (raiz.TryGetProperty("error", out JsonElement error))
                {
                    throw new InvalidOperationException(
                        "Ollama respondió con un error: " +
                        error.ToString());
                }

                if (raiz.TryGetProperty("message", out JsonElement mensaje) &&
                    mensaje.TryGetProperty("content", out JsonElement parte))
                {
                    string trozo = parte.GetString() ?? string.Empty;

                    if (trozo.Length > 0)
                    {
                        acumulado.Append(trozo);

                        if (alProgreso != null)
                        {
                            string parcial =
                                LimpiarRespuesta(acumulado.ToString());

                            if (!string.IsNullOrWhiteSpace(parcial))
                                alProgreso(parcial);
                        }
                    }
                }

                if (raiz.TryGetProperty("done_reason", out JsonElement fin))
                {
                    motivo = fin.ToString();
                }
            }

            string texto = acumulado.ToString();

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new InvalidOperationException(
                    "Ollama devolvió una respuesta vacía.\n\n" +
                    $"Modelo: {NombreModelo}\n" +
                    $"Motivo de finalización: {motivo}\n" +
                    $"Caracteres del mensaje enviado: {mensajeUsuario.Length}");
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

            // La pantalla muestra texto plano: se quitan marcas de Markdown.
            texto = texto.Replace("**", string.Empty)
                         .Replace("__", string.Empty);

            texto = Regex.Replace(
                texto,
                @"(?m)^\s{0,3}#{1,6}\s*",
                string.Empty);

            return texto.Trim();
        }
    }
}

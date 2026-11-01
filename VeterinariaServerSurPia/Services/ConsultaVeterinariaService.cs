
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VeterinariaServerSurPia.Services
{
    /// <summary>
    /// Consultas generales de la veterinaria.
    /// Este servicio solo lee SQLite; no modifica ni elimina registros.
    /// </summary>
    public sealed class ConsultaVeterinariaService
    {
        private readonly DatabaseService _database;

        public sealed class MascotaCoincidencia
        {
            public string Id { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string Especie { get; set; } = "";
            public string Raza { get; set; } = "";
        }

        public ConsultaVeterinariaService(DatabaseService database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Busca mascotas cuyo nombre completo o ID aparezca
        /// expresamente en la pregunta del usuario.
        /// No utiliza coincidencias parciales de palabras.
        /// </summary>
        public async Task<List<MascotaCoincidencia>> BuscarMascotasMencionadasAsync(
            string pregunta)
        {
            List<MascotaCoincidencia> coincidencias = new();

            if (string.IsNullOrWhiteSpace(pregunta))
                return coincidencias;

            string preguntaNormalizada = NormalizarTexto(pregunta);

            using SqliteConnection connection = await _database.OpenConnectionAsync();
            using SqliteCommand command = connection.CreateCommand();

            command.CommandText = @"
                SELECT IdMascota, Nombre, Especie, Raza
                FROM Mascotas
                ORDER BY LENGTH(Nombre) DESC, Nombre ASC;";

            using SqliteDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string id = Valor(reader, 0);
                string nombre = Valor(reader, 1);

                bool coincideId = ContieneCoincidenciaExacta(
                    preguntaNormalizada,
                    NormalizarTexto(id));

                bool coincideNombre = ContieneCoincidenciaExacta(
                    preguntaNormalizada,
                    NormalizarTexto(nombre));

                if (coincideId || coincideNombre)
                {
                    coincidencias.Add(new MascotaCoincidencia
                    {
                        Id = id,
                        Nombre = nombre,
                        Especie = Valor(reader, 2),
                        Raza = Valor(reader, 3)
                    });
                }
            }

            return coincidencias;
        }

        private static bool ContieneCoincidenciaExacta(
            string texto,
            string termino)
        {
            if (string.IsNullOrWhiteSpace(texto) ||
                string.IsNullOrWhiteSpace(termino) ||
                termino == "no registrado")
                return false;

            int posicion = 0;

            while ((posicion = texto.IndexOf(
                termino,
                posicion,
                StringComparison.Ordinal)) >= 0)
            {
                int inicio = posicion;
                int fin = posicion + termino.Length;

                bool limiteInicial =
                    inicio == 0 || !char.IsLetterOrDigit(texto[inicio - 1]);

                bool limiteFinal =
                    fin == texto.Length || !char.IsLetterOrDigit(texto[fin]);

                if (limiteInicial && limiteFinal)
                    return true;

                posicion++;
            }

            return false;
        }

        private static string NormalizarTexto(string texto)
        {
            string normalizado = texto
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            StringBuilder resultado = new();

            foreach (char caracter in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caracter) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    resultado.Append(caracter);
                }
            }

            return Regex.Replace(
                resultado.ToString().Normalize(NormalizationForm.FormC),
                @"\s+",
                " ").Trim();
        }

        public async Task<string> ObtenerResumenGeneralAsync()
        {
            using SqliteConnection connection = await _database.OpenConnectionAsync();

            long clientes = await ContarAsync(connection, "Clientes");
            long mascotas = await ContarAsync(connection, "Mascotas");
            long citas = await ContarAsync(connection, "Citas");
            long historiales = await ContarAsync(connection, "HistorialMedico");
            long cartillas = await ContarAsync(connection, "CartillaMedica");

            StringBuilder resultado = new();
            resultado.AppendLine("RESUMEN GENERAL DE LA VETERINARIA");
            resultado.AppendLine($"Clientes registrados: {clientes}");
            resultado.AppendLine($"Mascotas registradas: {mascotas}");
            resultado.AppendLine($"Citas registradas: {citas}");
            resultado.AppendLine($"Registros de historial médico: {historiales}");
            resultado.AppendLine($"Registros de cartilla médica: {cartillas}");
            resultado.AppendLine();
            resultado.AppendLine("Estos números corresponden únicamente a los registros guardados en SQLite.");

            return resultado.ToString();
        }

        public async Task<string> ObtenerProximasCitasAsync(int limite = 30)
        {
            limite = Math.Clamp(limite, 1, 200);

            using SqliteConnection connection = await _database.OpenConnectionAsync();
            using SqliteCommand command = connection.CreateCommand();

            string hoy = DateTime.Today.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            command.CommandText = @"
                SELECT c.IdCita, c.Fecha, c.Hora, c.Servicio, c.Observaciones,
                       m.IdMascota, m.Nombre, m.Especie, cl.Nombre
                FROM Citas c
                INNER JOIN Mascotas m ON m.IdMascota = c.IdMascota
                LEFT JOIN Clientes cl ON cl.IdCliente = m.IdCliente
                WHERE date(c.Fecha) >= date($hoy)
                ORDER BY date(c.Fecha) ASC, c.Hora ASC
                LIMIT $limite;";

            command.Parameters.AddWithValue("$hoy", hoy);
            command.Parameters.AddWithValue("$limite", limite);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();

            StringBuilder resultado = new();
            resultado.AppendLine("PRÓXIMAS CITAS REGISTRADAS");
            resultado.AppendLine($"Fecha de consulta: {DateTime.Today:dd/MM/yyyy}");
            resultado.AppendLine();

            int cantidad = 0;

            while (await reader.ReadAsync())
            {
                cantidad++;

                resultado.AppendLine($"Cita {cantidad}:");
                resultado.AppendLine($"ID de cita: {Valor(reader, 0)}");
                resultado.AppendLine($"Fecha: {FormatearFecha(Valor(reader, 1))}");
                resultado.AppendLine($"Hora: {Valor(reader, 2)}");
                resultado.AppendLine($"Servicio: {Valor(reader, 3)}");
                resultado.AppendLine($"Observaciones: {Valor(reader, 4)}");
                resultado.AppendLine($"Mascota: {Valor(reader, 6)} (ID: {Valor(reader, 5)})");
                resultado.AppendLine($"Especie: {Valor(reader, 7)}");
                resultado.AppendLine($"Propietario: {Valor(reader, 8)}");
                resultado.AppendLine("------------------------------");
            }

            if (cantidad == 0)
            {
                resultado.AppendLine("No se encontraron citas desde hoy en adelante.");
                resultado.AppendLine("Esto indica que no hay citas futuras registradas en SQLite; no confirma que no existan citas fuera del sistema.");
            }
            else
            {
                resultado.AppendLine($"Total de citas mostradas: {cantidad}.");

                if (cantidad == limite)
                    resultado.AppendLine($"Se alcanzó el límite de consulta de {limite} citas. Puede haber más registros.");
            }

            return resultado.ToString();
        }

        public async Task<string> ObtenerProximasFechasCartillaAsync(int limite = 50)
        {
            limite = Math.Clamp(limite, 1, 200);

            using SqliteConnection connection = await _database.OpenConnectionAsync();
            using SqliteCommand command = connection.CreateCommand();

            string hoy = DateTime.Today.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            command.CommandText = @"
                SELECT cm.Tipo, cm.Producto, cm.FechaAplicacion, cm.ProximaFecha,
                       cm.Observaciones, m.IdMascota, m.Nombre, m.Especie, cl.Nombre
                FROM CartillaMedica cm
                INNER JOIN Mascotas m ON m.IdMascota = cm.IdMascota
                LEFT JOIN Clientes cl ON cl.IdCliente = m.IdCliente
                WHERE trim(COALESCE(cm.ProximaFecha, '')) <> ''
                  AND date(cm.ProximaFecha) >= date($hoy)
                ORDER BY date(cm.ProximaFecha) ASC
                LIMIT $limite;";

            command.Parameters.AddWithValue("$hoy", hoy);
            command.Parameters.AddWithValue("$limite", limite);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();

            StringBuilder resultado = new();
            resultado.AppendLine("PRÓXIMAS FECHAS REGISTRADAS EN CARTILLA MÉDICA");
            resultado.AppendLine($"Fecha de consulta: {DateTime.Today:dd/MM/yyyy}");
            resultado.AppendLine();

            int cantidad = 0;

            while (await reader.ReadAsync())
            {
                cantidad++;

                resultado.AppendLine($"Registro {cantidad}:");
                resultado.AppendLine($"Tipo: {Valor(reader, 0)}");
                resultado.AppendLine($"Producto: {Valor(reader, 1)}");
                resultado.AppendLine($"Fecha de aplicación: {FormatearFecha(Valor(reader, 2))}");
                resultado.AppendLine($"Próxima fecha registrada: {FormatearFecha(Valor(reader, 3))}");
                resultado.AppendLine($"Observaciones: {Valor(reader, 4)}");
                resultado.AppendLine($"Mascota: {Valor(reader, 6)} (ID: {Valor(reader, 5)})");
                resultado.AppendLine($"Especie: {Valor(reader, 7)}");
                resultado.AppendLine($"Propietario: {Valor(reader, 8)}");
                resultado.AppendLine("------------------------------");
            }

            if (cantidad == 0)
            {
                resultado.AppendLine("No se encontraron próximas fechas en la cartilla médica.");
                resultado.AppendLine("Esto no demuestra que las mascotas no necesiten vacunas o refuerzos; solo indica que no hay próximas fechas registradas que coincidan con la consulta.");
            }
            else
            {
                resultado.AppendLine($"Total de registros mostrados: {cantidad}.");

                if (cantidad == limite)
                    resultado.AppendLine($"Se alcanzó el límite de consulta de {limite} registros. Puede haber más.");
            }

            return resultado.ToString();
        }

        public async Task<string> ObtenerListadoMascotasAsync(int limite = 100)
        {
            limite = Math.Clamp(limite, 1, 500);

            using SqliteConnection connection = await _database.OpenConnectionAsync();
            using SqliteCommand command = connection.CreateCommand();

            command.CommandText = @"
                SELECT m.IdMascota, m.Nombre, m.Especie, m.Raza, m.Edad,
                       m.Sexo, m.Peso, cl.Nombre, cl.Telefono
                FROM Mascotas m
                LEFT JOIN Clientes cl ON cl.IdCliente = m.IdCliente
                ORDER BY m.Nombre
                LIMIT $limite;";

            command.Parameters.AddWithValue("$limite", limite);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();

            StringBuilder resultado = new();
            resultado.AppendLine("MASCOTAS REGISTRADAS");
            resultado.AppendLine();

            int cantidad = 0;

            while (await reader.ReadAsync())
            {
                cantidad++;

                resultado.AppendLine($"{cantidad}. {Valor(reader, 1)} (ID: {Valor(reader, 0)})");
                resultado.AppendLine($"   Especie: {Valor(reader, 2)}");
                resultado.AppendLine($"   Raza: {Valor(reader, 3)}");
                resultado.AppendLine($"   Edad registrada: {Valor(reader, 4)}");
                resultado.AppendLine($"   Sexo: {Valor(reader, 5)}");
                resultado.AppendLine($"   Peso registrado: {Valor(reader, 6)} kg");
                resultado.AppendLine($"   Propietario: {Valor(reader, 7)}");
                resultado.AppendLine($"   Teléfono del propietario: {Valor(reader, 8)}");
            }

            if (cantidad == 0)
            {
                resultado.AppendLine("No hay mascotas registradas en SQLite.");
            }
            else if (cantidad == limite)
            {
                resultado.AppendLine($"Se muestran hasta {limite} mascotas; puede haber más registros.");
            }

            return resultado.ToString();
        }

        private static async Task<long> ContarAsync(
            SqliteConnection connection,
            string tabla)
        {
            string[] tablasPermitidas =
            {
                "Clientes",
                "Mascotas",
                "Citas",
                "HistorialMedico",
                "CartillaMedica"
            };

            if (Array.IndexOf(tablasPermitidas, tabla) < 0)
                throw new ArgumentException("Tabla no permitida.", nameof(tabla));

            using SqliteCommand command = connection.CreateCommand();

            command.CommandText = $"SELECT COUNT(*) FROM {tabla};";

            object? valor = await command.ExecuteScalarAsync();

            return Convert.ToInt64(
                valor,
                CultureInfo.InvariantCulture);
        }

        private static string Valor(
            SqliteDataReader reader,
            int indice)
        {
            if (reader.IsDBNull(indice))
                return "No registrado";

            string valor = Convert.ToString(
                reader.GetValue(indice),
                CultureInfo.InvariantCulture) ?? "";

            return string.IsNullOrWhiteSpace(valor)
                ? "No registrado"
                : valor;
        }

        private static string FormatearFecha(string fecha)
        {
            if (string.IsNullOrWhiteSpace(fecha) ||
                fecha.Equals("No registrado", StringComparison.OrdinalIgnoreCase))
            {
                return "No registrada";
            }

            if (DateTime.TryParseExact(
                fecha,
                new[]
                {
                    "yyyy-MM-dd",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd/MM/yyyy",
                    "d/M/yyyy"
                },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime fechaConvertida))
            {
                return fechaConvertida.ToString(
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture);
            }

            return fecha;
        }
    }
}

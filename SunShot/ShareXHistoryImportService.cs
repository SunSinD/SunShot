// Copyright (c) 2026 SunSinD
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShareX
{
    internal static class ShareXHistoryImportService
    {
        private const long MaximumInput = 128L * 1024 * 1024;
        private const long MaximumText = 32L * 1024 * 1024;
        private const int MaximumRows = 50000;
        private const int MaximumField = 65536;
        private const string SourceError = "ShareX history could not be safely read. Close ShareX or database tools and retry, or clear Import capture history.";
        private const string DestinationError = "SunShot history is busy or has database sidecar files. Close its history windows and database tools, then restart SunShot before retrying. No sidecar files were removed.";
        private static readonly string[] Fields = { "FileName", "FilePath", "DateTime", "Type", "Host", "URL", "ThumbnailURL", "DeletionURL", "ShortenedURL", "Tags" };
        private const string Schema = @"CREATE TABLE History (
Id INTEGER PRIMARY KEY AUTOINCREMENT, FileName TEXT, FilePath TEXT, DateTime TEXT,
Type TEXT, Host TEXT, URL TEXT, ThumbnailURL TEXT, DeletionURL TEXT, ShortenedURL TEXT, Tags TEXT);";

        internal sealed class Row
        {
            private readonly string[] values;
            internal Row(string[] fields) { values = (string[])fields.Clone(); }
            internal string this[int index] => values[index];
            internal string Key
            {
                get
                {
                    string[] normalized = (string[])values.Clone();
                    DateTime date = DateTime.Parse(normalized[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    normalized[2] = date.Kind == DateTimeKind.Unspecified ? "unspecified:" + date.Ticks : "utc:" + date.ToUniversalTime().Ticks;
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(normalized));
                    return Convert.ToHexString(SHA256.HashData(bytes));
                }
            }
        }

        internal sealed class Payload
        {
            private readonly IReadOnlyList<Row> rows;
            internal Payload(IEnumerable<Row> values) { rows = Array.AsReadOnly(values.ToArray()); }
            internal int Count => rows.Count;
            internal IEnumerable<Row> Rows => rows;
        }

        internal static bool IsLocalAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' ||
                (path[2] != '\\' && path[2] != '/') || path.IndexOf(':', 2) >= 0 || path.IndexOf('\0') >= 0) return false;
            foreach (string part in path.Substring(3).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part != "." && part != ".." && part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
                string name = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
                if (name == "CON" || name == "PRN" || name == "AUX" || name == "NUL" ||
                    (name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) && name[3] >= '1' && name[3] <= '9')) return false;
            }
            try { return Path.IsPathFullyQualified(path) && Path.GetFullPath(path).StartsWith(path.Substring(0, 2), StringComparison.OrdinalIgnoreCase); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException) { return false; }
        }

        internal static void ValidateSize(FileStream stream)
        {
            if (stream.Length == 0 || stream.Length > MaximumInput) throw new InvalidDataException("History exceeds the supported size limit or is empty.");
        }

        internal static void ValidateDestination(string folder)
        {
            string path = Path.Combine(folder, "History.db");
            ShareXSettingsImportService.ValidateRegularTarget(path);
            foreach (string suffix in new[] { "-wal", "-shm", "-journal" })
            {
                string sidecar = path + suffix;
                ShareXSettingsImportService.EnsureNoLinks(sidecar);
                if (File.Exists(sidecar) || Directory.Exists(sidecar)) throw new InvalidDataException(DestinationError);
            }
        }

        internal static Payload Prepare(string folder, List<string> warnings)
        {
            string database = Path.Combine(folder, "History.db");
            string legacy = Path.Combine(folder, "History.json");
            ShareXSettingsImportService.ValidateRegularTarget(database);
            ShareXSettingsImportService.ValidateRegularTarget(legacy);
            if (!File.Exists(database) && !File.Exists(legacy))
            {
                warnings.Add("No ShareX history found. SunShot history was kept.");
                return null;
            }
            try
            {
                if (!File.Exists(database)) return ReadLegacy(legacy, warnings);
                string temporary = Path.Combine(Path.GetTempPath(), "SunShot-history-import-" + Guid.NewGuid().ToString("N"));
                ShareXSettingsImportService.EnsureNoLinks(temporary);
                Directory.CreateDirectory(temporary);
                try
                {
                    string copy = Path.Combine(temporary, "History.db");
                    Snapshot(database, copy);
                    using SqliteConnection connection = Open(copy, SqliteOpenMode.ReadWrite);
                    CheckDatabase(connection);
                    // Any WAL recovery/checkpoint affects only the owned copy.
                    using (SqliteCommand checkpoint = connection.CreateCommand())
                    {
                        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        using SqliteDataReader result = checkpoint.ExecuteReader();
                        if (result.Read() && result.GetInt64(0) != 0) throw new InvalidDataException(SourceError);
                    }
                    return new Payload(ReadRows(connection, true, warnings));
                }
                finally
                {
                    ShareXSettingsImportService.EnsureNoLinks(temporary);
                    Directory.Delete(temporary, true);
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is SqliteException || error is JsonException ||
                error is UnauthorizedAccessException || error is ArgumentException || error is OverflowException || error is FormatException)
            {
                throw new InvalidDataException(SourceError);
            }
        }

        private static void Snapshot(string source, string destination)
        {
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" }) ShareXSettingsImportService.ValidateRegularTarget(source + suffix);
            if (File.Exists(source + "-journal")) throw new InvalidDataException(SourceError);
            // On Windows, these handles are incompatible with any open write handle.
            // Keep both handles alive until every byte has been copied.
            using FileStream database = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateSize(database);
            using FileStream wal = File.Exists(source + "-wal") ? new FileStream(source + "-wal", FileMode.Open, FileAccess.Read, FileShare.Read) : null;
            if (database.Length + (wal?.Length ?? 0) > MaximumInput) throw new InvalidDataException(SourceError);
            byte[] header = new byte[100];
            database.ReadExactly(header);
            if (Encoding.ASCII.GetString(header, 0, 16) != "SQLite format 3\0" ||
                !((header[18] == 1 && header[19] == 1) || (header[18] == 2 && header[19] == 2))) throw new InvalidDataException(SourceError);
            if (wal != null && wal.Length > 0)
            {
                if (header[18] != 2) throw new InvalidDataException(SourceError);
                int pageSize = (header[16] << 8) | header[17];
                ValidateWal(wal, pageSize == 1 ? 65536 : pageSize);
            }
            database.Position = 0;
            CopyStream(database, destination);
            if (wal != null && wal.Length > 0)
            {
                wal.Position = 0;
                CopyStream(wal, destination + "-wal");
            }
            if (File.Exists(source + "-journal") || File.Exists(source + "-wal") != (wal != null)) throw new InvalidDataException(SourceError);
        }

        private static void CopyStream(FileStream source, string destination)
        {
            using FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(output);
            output.Flush(true);
        }

        private static uint Word(byte[] bytes, int offset, bool bigEndian) => bigEndian
            ? ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3]
            : ((uint)bytes[offset + 3] << 24) | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 1] << 8) | bytes[offset];

        private static void Checksum(byte[] bytes, int offset, int count, bool bigEndian, ref uint first, ref uint second)
        {
            unchecked
            {
                for (int i = offset; i < offset + count; i += 8)
                {
                    first += Word(bytes, i, bigEndian) + second;
                    second += Word(bytes, i + 4, bigEndian) + first;
                }
            }
        }

        private static void ValidateWal(FileStream wal, int pageSize)
        {
            if (wal.Length < 32 || pageSize < 512 || pageSize > 65536 || (pageSize & (pageSize - 1)) != 0 ||
                (wal.Length - 32) % (pageSize + 24) != 0) throw new InvalidDataException(SourceError);
            byte[] header = new byte[32];
            wal.ReadExactly(header);
            uint magic = Word(header, 0, true);
            if ((magic != 0x377f0682 && magic != 0x377f0683) || Word(header, 4, true) != 3007000 || Word(header, 8, true) != pageSize)
                throw new InvalidDataException(SourceError);
            bool bigEndian = magic == 0x377f0683;
            uint first = 0, second = 0;
            Checksum(header, 0, 24, bigEndian, ref first, ref second);
            if (first != Word(header, 24, true) || second != Word(header, 28, true)) throw new InvalidDataException(SourceError);
            byte[] frame = new byte[pageSize + 24];
            uint commit = 1;
            while (wal.Position < wal.Length)
            {
                wal.ReadExactly(frame);
                if (Word(frame, 0, true) == 0 || Word(frame, 8, true) != Word(header, 16, true) || Word(frame, 12, true) != Word(header, 20, true))
                    throw new InvalidDataException(SourceError);
                Checksum(frame, 0, 8, bigEndian, ref first, ref second);
                Checksum(frame, 24, pageSize, bigEndian, ref first, ref second);
                if (first != Word(frame, 16, true) || second != Word(frame, 20, true)) throw new InvalidDataException(SourceError);
                commit = Word(frame, 4, true);
            }
            if (commit == 0) throw new InvalidDataException(SourceError);
        }

        private static SqliteConnection Open(string path, SqliteOpenMode mode)
        {
            SqliteConnection connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 1
            }.ToString());
            try
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "PRAGMA trusted_schema=OFF; PRAGMA busy_timeout=0;";
                command.ExecuteNonQuery();
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        private static void CheckDatabase(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            if (!string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.Ordinal)) throw new InvalidDataException(SourceError);
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE (type='table' AND name NOT IN ('History','sqlite_sequence')) OR type IN ('view','trigger');";
            if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0) throw new InvalidDataException(SourceError);
            command.CommandText = "PRAGMA table_xinfo(History);";
            using SqliteDataReader reader = command.ExecuteReader();
            List<string> names = new List<string>();
            while (reader.Read())
            {
                string name = reader.GetString(1);
                string type = reader.GetString(2);
                if (type != (name == "Id" ? "INTEGER" : "TEXT") || reader.GetInt64(5) != (name == "Id" ? 1 : 0) || reader.GetInt64(6) != 0)
                    throw new InvalidDataException(SourceError);
                names.Add(name);
            }
            if (!names.SequenceEqual(new[] { "Id" }.Concat(Fields))) throw new InvalidDataException(SourceError);
        }

        private static List<Row> ReadRows(SqliteConnection connection, bool incoming, List<string> warnings)
        {
            List<Row> rows = new List<Row>();
            int invalid = 0, clearedPaths = 0, clearedUrls = 0, count = 0;
            long text = 0;
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT " + string.Join(",", Fields.Select(field => "\"" + field + "\", length(\"" + field + "\")")) + " FROM History ORDER BY Id;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (++count > MaximumRows) throw new InvalidDataException("History contains too many records for one import.");
                string[] values = new string[Fields.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    if (reader.IsDBNull(i * 2)) { values[i] = ""; continue; }
                    long length = reader.GetInt64(i * 2 + 1);
                    text += length;
                    if (length > MaximumField || text > MaximumText || reader.GetFieldType(i * 2) != typeof(string)) throw new InvalidDataException(SourceError);
                    values[i] = reader.GetString(i * 2);
                }
                Row row = Normalize(values, incoming, ref clearedPaths, ref clearedUrls);
                if (row == null)
                {
                    if (!incoming) throw new InvalidDataException("Existing SunShot history contains invalid records. Repair it before importing.");
                    invalid++;
                }
                else rows.Add(row);
            }
            AddWarnings(warnings, invalid, clearedPaths, clearedUrls);
            return rows;
        }

        private static Row Normalize(string[] values, bool incoming, ref int clearedPaths, ref int clearedUrls)
        {
            if (!DateTime.TryParseExact(values[2], new[] { "o", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK" },
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)) return null;
            values[2] = date.ToString("o", CultureInfo.InvariantCulture);
            try
            {
                JObject tags = string.IsNullOrEmpty(values[9]) || values[9] == "null" ? new JObject() : ParseObject(values[9]);
                if (tags.Count > 256 || tags.Properties().Any(p => p.Name.StartsWith("$", StringComparison.Ordinal) ||
                    (p.Value.Type != JTokenType.String && p.Value.Type != JTokenType.Null))) return null;
                values[9] = new JObject(tags.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, p.Value))).ToString(Formatting.None);
            }
            catch (JsonException) { return null; }
            if (incoming)
            {
                if (values[1].Length > 0 && !IsLocalAbsolutePath(values[1])) { values[1] = ""; clearedPaths++; }
                bool cleared = false;
                for (int i = 5; i <= 8; i++)
                {
                    if (values[i].Length > 0 && (!Uri.TryCreate(values[i], UriKind.Absolute, out Uri uri) || (uri.Scheme != "https" && uri.Scheme != "http")))
                    { values[i] = ""; cleared = true; }
                }
                if (cleared) clearedUrls++;
            }
            return new Row(values);
        }

        private static JObject ParseObject(string text)
        {
            using StringReader input = new StringReader(text);
            using JsonTextReader reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
            JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new JsonReaderException();
            return value;
        }

        private static Payload ReadLegacy(string path, List<string> warnings)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumText) throw new InvalidDataException(SourceError);
            using StreamReader input = new StreamReader(stream, Encoding.UTF8, true);
            // ShareX's legacy format is comma-separated objects without an array wrapper.
            string text = input.ReadToEnd();
            using StringReader wrapped = new StringReader("[" + text + "]");
            using JsonTextReader reader = new JsonTextReader(wrapped) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
            if (!reader.Read() || reader.TokenType != JsonToken.StartArray) throw new InvalidDataException(SourceError);
            List<Row> rows = new List<Row>();
            int count = 0, invalid = 0, clearedPaths = 0, clearedUrls = 0;
            long total = 0;
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                if (++count > MaximumRows || reader.TokenType != JsonToken.StartObject) throw new InvalidDataException(SourceError);
                JObject obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (obj.Descendants().OfType<JProperty>().Any(p => p.Name.StartsWith("$", StringComparison.Ordinal))) throw new InvalidDataException(SourceError);
                string[] values = new string[Fields.Length];
                bool valid = true;
                for (int i = 0; i < Fields.Length; i++)
                {
                    JToken value = obj[Fields[i]];
                    if (i == 9) values[i] = value == null || value.Type == JTokenType.Null ? "" : value.ToString(Formatting.None);
                    else if (value == null || value.Type == JTokenType.Null) values[i] = "";
                    else if (value.Type == JTokenType.String) values[i] = (string)value;
                    else { valid = false; values[i] = ""; }
                    total += values[i].Length;
                    if (values[i].Length > MaximumField || total > MaximumText) throw new InvalidDataException(SourceError);
                }
                Row row = valid ? Normalize(values, true, ref clearedPaths, ref clearedUrls) : null;
                if (row == null) invalid++; else rows.Add(row);
            }
            if (reader.TokenType != JsonToken.EndArray || reader.Read()) throw new InvalidDataException(SourceError);
            AddWarnings(warnings, invalid, clearedPaths, clearedUrls);
            warnings.Add("Legacy ShareX history imported. Original files stay in place.");
            return new Payload(rows);
        }

        private static void AddWarnings(List<string> warnings, int invalid, int paths, int urls)
        {
            if (invalid > 0) warnings.Add($"History: skipped {invalid} invalid records (date or tags).");
            if (paths > 0) warnings.Add($"History: cleared local paths in {paths} records.");
            if (urls > 0) warnings.Add($"History: cleared unsupported URLs in {urls} records.");
        }

        internal static void StageMerge(Payload incoming, string original, string staged)
        {
            ShareXSettingsImportService.ValidateRegularTarget(original);
            ShareXSettingsImportService.ValidateRegularTarget(staged);
            bool existing = File.Exists(original);
            if (existing)
            {
                using FileStream input = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read);
                ValidateSize(input);
                CopyStream(input, staged);
            }
            try
            {
                using (SqliteConnection connection = Open(staged, existing ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate))
                {
                    if (existing) CheckDatabase(connection);
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;";
                    command.ExecuteNonQuery();
                    if (!existing) { command.CommandText = Schema; command.ExecuteNonQuery(); }
                    List<Row> existingRows = ReadRows(connection, false, new List<string>());
                    HashSet<string> keys = new HashSet<string>(existingRows.Select(row => row.Key), StringComparer.Ordinal);
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO History (" + string.Join(",", Fields.Select(f => "\"" + f + "\"")) + ") VALUES (" + string.Join(",", Fields.Select((f, i) => "@p" + i)) + ");";
                    for (int i = 0; i < Fields.Length; i++) command.Parameters.AddWithValue("@p" + i, "");
                    int mergedCount = existingRows.Count;
                    long addedText = existingRows.Sum(row => Fields.Select((field, i) => (long)row[i].Length).Sum());
                    foreach (Row row in incoming.Rows)
                    {
                        if (!keys.Add(row.Key)) continue;
                        if (++mergedCount > MaximumRows) throw new InvalidDataException("Merged history contains too many records for one import.");
                        for (int i = 0; i < Fields.Length; i++)
                        {
                            command.Parameters[i].Value = row[i];
                            addedText += row[i].Length;
                        }
                        if (addedText > MaximumText) throw new InvalidDataException("Merged history exceeds the supported text limit.");
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                    CheckDatabase(connection);
                }
                using FileStream durable = new FileStream(staged, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (durable.Length > MaximumInput) throw new InvalidDataException("Merged history exceeds the supported size limit.");
                durable.Flush(true);
            }
            catch (SqliteException) { throw new InvalidDataException("SunShot history could not be merged safely. Existing history has been kept."); }
        }

        internal static void ValidateBackup(string original)
        {
            string folder = Path.Combine(Path.GetTempPath(), "SunShot-history-recovery-" + Guid.NewGuid().ToString("N"));
            ShareXSettingsImportService.EnsureNoLinks(folder);
            Directory.CreateDirectory(folder);
            try
            {
                StageMerge(new Payload(Array.Empty<Row>()), original, Path.Combine(folder, "History.db"));
            }
            catch (Exception error) when (error is IOException || error is SqliteException || error is InvalidDataException)
            {
                throw new InvalidDataException("The history backup is damaged. Keep the backup and retry recovery.");
            }
            finally
            {
                ShareXSettingsImportService.EnsureNoLinks(folder);
                Directory.Delete(folder, true);
            }
        }
    }
}

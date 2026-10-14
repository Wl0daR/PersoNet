using System.Globalization;
using System.Text;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>A row of values for one table in an SQL dump, with the line it starts on.</summary>
public sealed record SqlRow(int Line, IReadOnlyDictionary<string, string?> Values);

/// <summary>
/// Reads the rows of one table from a MySQL dump (mysqldump, phpMyAdmin, HeidiSQL): <c>INSERT</c> and <c>REPLACE</c>
/// statements, with or without a column list and with many rows each. Without a column list the columns are taken from
/// the table's <c>CREATE TABLE</c>. Comments and other statements are skipped; nothing is executed.
/// </summary>
public static class SqlDumpReader
{
    private static readonly HashSet<string> NotColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRIMARY", "KEY", "UNIQUE", "INDEX", "CONSTRAINT", "FOREIGN", "FULLTEXT", "SPATIAL", "CHECK",
    };

    /// <summary>The rows of <paramref name="table"/> (case-insensitive); values keyed by column name, case-insensitive.</summary>
    public static IReadOnlyList<SqlRow> ReadTable(string sql, string table)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var tokens = Tokenize(sql);
        var lines = LineStarts(sql);
        var rows = new List<SqlRow>();
        List<string>? declared = null;

        foreach (var statement in Statements(tokens))
        {
            if (IsCreateTable(statement, table, out var columns))
            {
                declared = columns;
            }
            else if (IsInsert(statement, table, out var start))
            {
                ReadInsert(statement, start, declared, lines, rows);
            }
        }

        return rows;
    }

    private static void ReadInsert(List<Token> statement, int index, List<string>? declared, List<int> lines, List<SqlRow> rows)
    {
        // Optional column list, then VALUES.
        List<string>? columns = null;
        if (index < statement.Count && statement[index].IsSymbol('('))
        {
            columns = [];
            index++;
            while (index < statement.Count && !statement[index].IsSymbol(')'))
            {
                if (statement[index].Kind is TokenKind.Identifier or TokenKind.Word)
                {
                    columns.Add(statement[index].Text);
                }

                index++;
            }

            index++;
        }

        if (index >= statement.Count || !(statement[index].IsWord("VALUES") || statement[index].IsWord("VALUE")))
        {
            return;
        }

        columns ??= declared;
        if (columns is null)
        {
            throw new FormatException("Plik nie ma nazw kolumn ani polecenia CREATE TABLE, więc nie wiadomo, która wartość jest którą.");
        }

        index++;
        while (index < statement.Count)
        {
            if (!statement[index].IsSymbol('('))
            {
                index++;
                continue;
            }

            var line = LineOf(lines, statement[index].Offset);
            var values = new List<string?>();
            index++;
            var expectValue = true;
            while (index < statement.Count && !statement[index].IsSymbol(')'))
            {
                var token = statement[index];
                if (token.IsSymbol(','))
                {
                    if (expectValue)
                    {
                        values.Add(null);
                    }

                    expectValue = true;
                }
                else if (expectValue)
                {
                    values.Add(token.Kind == TokenKind.Word && token.Text.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? null : token.Text);
                    expectValue = false;
                }

                index++;
            }

            index++;
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count && i < values.Count; i++)
            {
                row[columns[i]] = values[i];
            }

            rows.Add(new SqlRow(line, row));
        }
    }

    // INSERT [LOW_PRIORITY | DELAYED | HIGH_PRIORITY] [IGNORE] [INTO] [db.]table ...; returns the index after the table name.
    private static bool IsInsert(List<Token> statement, string table, out int next)
    {
        next = 0;
        if (statement.Count < 3 || !(statement[0].IsWord("INSERT") || statement[0].IsWord("REPLACE")))
        {
            return false;
        }

        var index = 1;
        while (index < statement.Count && statement[index].Kind == TokenKind.Word
            && (statement[index].IsWord("LOW_PRIORITY") || statement[index].IsWord("DELAYED") || statement[index].IsWord("HIGH_PRIORITY")
                || statement[index].IsWord("IGNORE") || statement[index].IsWord("INTO")))
        {
            index++;
        }

        return TableName(statement, ref index, table) && (next = index) > 0;
    }

    private static bool IsCreateTable(List<Token> statement, string table, out List<string> columns)
    {
        columns = [];
        if (statement.Count < 4 || !statement[0].IsWord("CREATE"))
        {
            return false;
        }

        var index = 1;
        while (index < statement.Count && statement[index].Kind == TokenKind.Word && !statement[index].IsWord("TABLE"))
        {
            index++; // TEMPORARY
        }

        if (index >= statement.Count || !statement[index].IsWord("TABLE"))
        {
            return false;
        }

        index++;
        while (index < statement.Count && (statement[index].IsWord("IF") || statement[index].IsWord("NOT") || statement[index].IsWord("EXISTS")))
        {
            index++;
        }

        if (!TableName(statement, ref index, table) || index >= statement.Count || !statement[index].IsSymbol('('))
        {
            return false;
        }

        // A column definition starts right after the opening bracket or a comma at the outer level.
        var depth = 0;
        var atStart = false;
        for (; index < statement.Count; index++)
        {
            var token = statement[index];
            if (token.IsSymbol('('))
            {
                depth++;
                atStart = depth == 1;
                continue;
            }

            if (token.IsSymbol(')'))
            {
                if (--depth == 0)
                {
                    break;
                }

                continue;
            }

            if (depth == 1 && token.IsSymbol(','))
            {
                atStart = true;
                continue;
            }

            if (atStart && token.Kind is TokenKind.Identifier or TokenKind.Word && !(token.Kind == TokenKind.Word && NotColumns.Contains(token.Text)))
            {
                columns.Add(token.Text);
            }

            atStart = false;
        }

        return columns.Count > 0;
    }

    // [db.]table, where either part may be quoted with backticks.
    private static bool TableName(List<Token> statement, ref int index, string table)
    {
        if (index >= statement.Count || statement[index].Kind is not (TokenKind.Identifier or TokenKind.Word))
        {
            return false;
        }

        var name = statement[index].Text;
        index++;
        if (index + 1 < statement.Count && statement[index].IsSymbol('.'))
        {
            name = statement[index + 1].Text;
            index += 2;
        }
        else if (statement[index - 1].Kind == TokenKind.Word && name.Contains('.', StringComparison.Ordinal))
        {
            name = name[(name.LastIndexOf('.') + 1)..];
        }

        return name.Equals(table, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<List<Token>> Statements(List<Token> tokens)
    {
        var current = new List<Token>();
        foreach (var token in tokens)
        {
            if (token.IsSymbol(';'))
            {
                if (current.Count > 0)
                {
                    yield return current;
                }

                current = [];
            }
            else
            {
                current.Add(token);
            }
        }

        if (current.Count > 0)
        {
            yield return current;
        }
    }

    private static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '#' || (c == '-' && i + 2 <= sql.Length && sql.AsSpan(i).StartsWith("--") && (i + 2 == sql.Length || char.IsWhiteSpace(sql[i + 2]))))
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? sql.Length : end + 2;
            }
            else if (c is '\'' or '"' or '`')
            {
                var start = i;
                var text = Quoted(sql, ref i, c);
                tokens.Add(new Token(c == '`' ? TokenKind.Identifier : TokenKind.String, text, start));
            }
            else if (IsWordChar(c) || (c == '-' && i + 1 < sql.Length && char.IsAsciiDigit(sql[i + 1])))
            {
                var start = i;
                i++;
                while (i < sql.Length && IsWordChar(sql[i]))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, sql[start..i], start));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Symbol, c.ToString(), i));
                i++;
            }
        }

        return tokens;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' or '.';

    // A quoted value: backslash escapes and a doubled quote stand for the character itself.
    private static string Quoted(string sql, ref int i, char quote)
    {
        var text = new StringBuilder();
        i++;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (c == '\\' && quote != '`' && i + 1 < sql.Length)
            {
                text.Append(sql[i + 1] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '0' => '\0',
                    'Z' => '\u001A',
                    var other => other,
                });
                i += 2;
            }
            else if (c == quote)
            {
                if (i + 1 < sql.Length && sql[i + 1] == quote)
                {
                    text.Append(quote);
                    i += 2;
                }
                else
                {
                    i++;
                    return text.ToString();
                }
            }
            else
            {
                text.Append(c);
                i++;
            }
        }

        return text.ToString();
    }

    private static List<int> LineStarts(string sql)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static int LineOf(List<int> starts, int offset)
    {
        var index = starts.BinarySearch(offset);
        return (index >= 0 ? index : ~index - 1) + 1;
    }

    private enum TokenKind
    {
        Word,
        String,
        Identifier,
        Symbol,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Offset)
    {
        public bool IsSymbol(char symbol) => Kind == TokenKind.Symbol && Text[0] == symbol;

        public bool IsWord(string word) => Kind == TokenKind.Word && Text.Equals(word, StringComparison.OrdinalIgnoreCase);

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Kind} {Text}");
    }
}

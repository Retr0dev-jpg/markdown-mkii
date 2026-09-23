using System.Text;

namespace MarkdownMkII.Core.Text;

internal static class DelimitedText
{
    public static List<List<string>> Parse(string source, bool trimCells = false)
    {
        source = (source ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var separator = Separator(source);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        void EndCell()
        {
            row.Add(trimCells ? cell.ToString().Trim() : cell.ToString());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();
            if (row.Any(value => value.Length > 0))
            {
                rows.Add(row);
            }

            row = [];
        }

        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(ch);
                }
            }
            else if (ch == '"' && cell.Length == 0)
            {
                quoted = true;
            }
            else if (ch == separator)
            {
                EndCell();
            }
            else if (ch == '\n')
            {
                EndRow();
            }
            else
            {
                cell.Append(ch);
            }
        }

        EndRow();
        return rows;
    }

    private static char Separator(string source)
    {
        var quoted = false;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '"')
            {
                if (quoted && i + 1 < source.Length && source[i + 1] == '"')
                {
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (!quoted && source[i] == '\t')
            {
                return '\t';
            }
            else if (!quoted && source[i] == '\n')
            {
                break;
            }
        }

        return ',';
    }
}

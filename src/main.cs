using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

namespace CSharpToHtmlCli
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] != "/convert")
            {
                ShowHelp();
                return;
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Lỗi: Thiếu tên file.");
                ShowHelp();
                return;
            }

            string filePath = args[1];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Lỗi: Không tìm thấy file '{filePath}'");
                return;
            }

            try
            {
                string sourceCode = File.ReadAllText(filePath);
                string htmlOutput = ConvertToHtml(sourceCode);
                
                string outputFile = Path.ChangeExtension(filePath, ".html");
                File.WriteAllText(outputFile, htmlOutput);
                
                Console.WriteLine($"✅ Thành công! Đã tạo file: {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi: {ex.Message}");
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("C# to HTML Converter CLI");
            Console.WriteLine("Cách dùng:");
            Console.WriteLine("  /convert <tên file>");
            Console.WriteLine();
            Console.WriteLine("Ví dụ:");
            Console.WriteLine("  /convert Program.cs");
        }

        static string ConvertToHtml(string code)
        {
            // Escape HTML entities
            code = HttpUtility.HtmlEncode(code);
            
            // Tô màu keywords
            string[] keywords = {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
                "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
                "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
                "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
                "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
                "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
                "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
                "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
                "void", "volatile", "while"
            };

            foreach (var keyword in keywords)
            {
                code = Regex.Replace(code, $@"\b{keyword}\b", 
                    $"<span style='color: #0000FF; font-weight: bold;'>{keyword}</span>", 
                    RegexOptions.IgnoreCase);
            }

            // Tô màu comments
            code = Regex.Replace(code, @"//.*$", 
                match => $"<span style='color: #008000;'>{match.Value}</span>", 
                RegexOptions.Multiline);
            
            code = Regex.Replace(code, @"/\*.*?\*/", 
                match => $"<span style='color: #008000;'>{match.Value}</span>", 
                RegexOptions.Singleline);

            // Tô màu strings
            code = Regex.Replace(code, @"""(\\""|[^""])*""", 
                match => $"<span style='color: #A31515;'>{match.Value}</span>");
            
            code = Regex.Replace(code, @"@""(\\""|[^""])*""", 
                match => $"<span style='color: #A31515;'>{match.Value}</span>");

            // Tô màu numbers
            code = Regex.Replace(code, @"\b\d+(\.\d+)?\b", 
                match => $"<span style='color: #098658;'>{match.Value}</span>");

            // Tạo HTML đầy đủ
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>C# Code</title>
    <style>
        body {{
            font-family: 'Consolas', 'Courier New', monospace;
            font-size: 14px;
            background-color: #f4f4f4;
            padding: 20px;
        }}
        pre {{
            background-color: #ffffff;
            border: 1px solid #ccc;
            border-radius: 5px;
            padding: 15px;
            overflow-x: auto;
            box-shadow: 2px 2px 5px rgba(0,0,0,0.1);
        }}
        code {{
            font-family: inherit;
        }}
    </style>
</head>
<body>
    <pre><code>{code}</code></pre>
</body>
</html>";
        }
    }
}

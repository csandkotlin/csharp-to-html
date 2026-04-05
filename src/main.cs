using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
#if NET472
using System.Web;
#else
using System.Net;
#endif

namespace CSharpToHtmlCli
{
    // State machine cho parsing
    enum ParseState
    {
        Normal,
        InString,
        InVerbatimString,
        InCharLiteral,
        InLineComment,
        InMultiLineComment
    }

    enum TokenType
    {
        Text,
        Keyword,
        Preprocessor,
        Comment,
        String,
        CharLiteral,
        Number
    }

    struct Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        
        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }

    class Program
    {
        private static readonly HashSet<string> _keywords = new HashSet<string>
        {
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

        private static readonly HashSet<string> _preprocessor = new HashSet<string>
        {
            "#if", "#else", "#elif", "#endif", "#define", "#undef",
            "#warning", "#error", "#line", "#region", "#endregion", "#pragma",
            "#nullable", "#pragma", "#r", "#load"
        };

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
            string outputPath = null;
            bool showLineNumbers = false;
            string theme = "auto";

            // Parse optional arguments
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "/out" && i + 1 < args.Length)
                {
                    outputPath = args[++i];
                }
                else if (args[i] == "/lines")
                {
                    showLineNumbers = true;
                }
                else if (args[i] == "/theme" && i + 1 < args.Length)
                {
                    theme = args[++i].ToLower();
                }
                else if (args[i] == "/help")
                {
                    ShowHelp();
                    return;
                }
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Lỗi: Không tìm thấy file '{filePath}'");
                return;
            }

            try
            {
                // Đọc file với encoding tự động phát hiện BOM
                string sourceCode = ReadFileWithEncoding(filePath);
                
                // Chuyển đổi sang HTML
                string htmlOutput = ConvertToHtml(sourceCode, showLineNumbers, theme);
                
                // Xác định output path
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = Path.ChangeExtension(filePath, ".html");
                }
                
                // Ghi file với UTF-8 encoding
                File.WriteAllText(outputPath, htmlOutput, Encoding.UTF8);
                
                Console.WriteLine($"✅ Thành công! Đã tạo file: {outputPath}");
                Console.WriteLine($"📊 Dung lượng: {htmlOutput.Length:N0} bytes");
                Console.WriteLine($"📝 Số dòng code: {sourceCode.Split('\n').Length}");
                Console.WriteLine($"🎨 Theme: {theme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Chi tiết: {ex.InnerException.Message}");
                }
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("=== C# to HTML Converter CLI ===");
            Console.WriteLine("Công cụ chuyển đổi file C# sang HTML với syntax highlighting");
            Console.WriteLine();
            Console.WriteLine("Cách dùng:");
            Console.WriteLine("  /convert <tên file> [tùy chọn]");
            Console.WriteLine();
            Console.WriteLine("Tùy chọn:");
            Console.WriteLine("  /out <đường dẫn>    - Đường dẫn file output (mặc định: cùng tên .html)");
            Console.WriteLine("  /lines              - Hiển thị số dòng");
            Console.WriteLine("  /theme <tên>        - Theme: light, dark, auto (mặc định: auto)");
            Console.WriteLine("  /help               - Hiển thị trợ giúp này");
            Console.WriteLine();
            Console.WriteLine("Ví dụ:");
            Console.WriteLine("  /convert Program.cs");
            Console.WriteLine("  /convert Program.cs /out output.html");
            Console.WriteLine("  /convert Program.cs /lines");
            Console.WriteLine("  /convert test.cs /out result.html /lines /theme dark");
        }

        static string ReadFileWithEncoding(string filePath)
        {
            // Đọc file với BOM detection
            byte[] fileBytes = File.ReadAllBytes(filePath);
            
            // Detect encoding
            if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
                return Encoding.UTF8.GetString(fileBytes, 3, fileBytes.Length - 3);
            if (fileBytes.Length >= 2 && fileBytes[0] == 0xFE && fileBytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(fileBytes, 2, fileBytes.Length - 2);
            if (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
                return Encoding.Unicode.GetString(fileBytes, 2, fileBytes.Length - 2);
            
            // Default to UTF-8
            return Encoding.UTF8.GetString(fileBytes);
        }

        static List<Token> ParseTokens(string code)
        {
            var tokens = new List<Token>();
            int pos = 0;
            int length = code.Length;
            ParseState state = ParseState.Normal;
            int tokenStart = 0;
            
            while (pos < length)
            {
                char current = code[pos];
                
                switch (state)
                {
                    case ParseState.Normal:
                        // Bắt đầu comment một dòng
                        if (current == '/' && pos + 1 < length && code[pos + 1] == '/')
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.InLineComment;
                            tokenStart = pos;
                            pos += 2;
                            continue;
                        }
                        // Bắt đầu comment nhiều dòng
                        else if (current == '/' && pos + 1 < length && code[pos + 1] == '*')
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.InMultiLineComment;
                            tokenStart = pos;
                            pos += 2;
                            continue;
                        }
                        // Bắt đầu string literal
                        else if (current == '"')
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.InString;
                            tokenStart = pos;
                            pos++;
                            continue;
                        }
                        // Bắt đầu verbatim string
                        else if (current == '@' && pos + 1 < length && code[pos + 1] == '"')
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.InVerbatimString;
                            tokenStart = pos;
                            pos += 2;
                            continue;
                        }
                        // Bắt đầu char literal
                        else if (current == '\'')
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.InCharLiteral;
                            tokenStart = pos;
                            pos++;
                            continue;
                        }
                        // Preprocessor directives
                        else if (current == '#' && (pos == 0 || code[pos - 1] == '\n'))
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            tokenStart = pos;
                            while (pos < length && code[pos] != '\n')
                                pos++;
                            string directive = code.Substring(tokenStart, pos - tokenStart);
                            tokens.Add(new Token(TokenType.Preprocessor, directive));
                            tokenStart = pos;
                            continue;
                        }
                        // Keyword/Identifier/Number detection
                        else if (char.IsLetter(current) || current == '_')
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            tokenStart = pos;
                            while (pos < length && (char.IsLetterOrDigit(code[pos]) || code[pos] == '_'))
                                pos++;
                            string word = code.Substring(tokenStart, pos - tokenStart);
                            tokens.Add(new Token(_keywords.Contains(word) ? TokenType.Keyword : TokenType.Text, word));
                            tokenStart = pos;
                            continue;
                        }
                        else if (char.IsDigit(current) || (current == '.' && pos + 1 < length && char.IsDigit(code[pos + 1])))
                        {
                            if (pos > tokenStart)
                                tokens.Add(new Token(TokenType.Text, code.Substring(tokenStart, pos - tokenStart)));
                            tokenStart = pos;
                            while (pos < length && (char.IsDigit(code[pos]) || code[pos] == '.' || 
                                   code[pos] == 'e' || code[pos] == 'E' || code[pos] == '+' || code[pos] == '-' ||
                                   char.ToLower(code[pos]) == 'f' || char.ToLower(code[pos]) == 'l' ||
                                   char.ToLower(code[pos]) == 'd' || char.ToLower(code[pos]) == 'm'))
                                pos++;
                            tokens.Add(new Token(TokenType.Number, code.Substring(tokenStart, pos - tokenStart)));
                            tokenStart = pos;
                            continue;
                        }
                        pos++;
                        break;
                        
                    case ParseState.InString:
                        // Xử lý escape sequence đúng cách
                        if (current == '\\' && pos + 1 < length)
                        {
                            pos += 2; // Bỏ qua cả \ và ký tự được escape
                            continue;
                        }
                        if (current == '"')
                        {
                            pos++;
                            tokens.Add(new Token(TokenType.String, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.Normal;
                            tokenStart = pos;
                            continue;
                        }
                        pos++;
                        break;
                        
                    case ParseState.InVerbatimString:
                        // Xử lý "" trong verbatim string
                        if (current == '"')
                        {
                            if (pos + 1 < length && code[pos + 1] == '"')
                            {
                                pos += 2; // Bỏ qua cả hai dấu ""
                                continue;
                            }
                            else
                            {
                                pos++;
                                tokens.Add(new Token(TokenType.String, code.Substring(tokenStart, pos - tokenStart)));
                                state = ParseState.Normal;
                                tokenStart = pos;
                                continue;
                            }
                        }
                        pos++;
                        break;
                        
                    case ParseState.InCharLiteral:
                        // Xử lý escape sequence trong char literal
                        if (current == '\\' && pos + 1 < length)
                        {
                            pos += 2;
                            continue;
                        }
                        if (current == '\'')
                        {
                            pos++;
                            tokens.Add(new Token(TokenType.CharLiteral, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.Normal;
                            tokenStart = pos;
                            continue;
                        }
                        pos++;
                        break;
                        
                    case ParseState.InLineComment:
                        if (current == '\n')
                        {
                            tokens.Add(new Token(TokenType.Comment, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.Normal;
                            tokenStart = pos;
                        }
                        pos++;
                        break;
                        
                    case ParseState.InMultiLineComment:
                        if (current == '*' && pos + 1 < length && code[pos + 1] == '/')
                        {
                            pos += 2;
                            tokens.Add(new Token(TokenType.Comment, code.Substring(tokenStart, pos - tokenStart)));
                            state = ParseState.Normal;
                            tokenStart = pos;
                            continue;
                        }
                        pos++;
                        break;
                }
            }
            
            // Kết thúc file, thêm token cuối cùng nếu cần
            if (tokenStart < length)
            {
                string remaining = code.Substring(tokenStart);
                if (state == ParseState.InLineComment || state == ParseState.InMultiLineComment)
                    tokens.Add(new Token(TokenType.Comment, remaining));
                else if (state == ParseState.InString || state == ParseState.InVerbatimString || state == ParseState.InCharLiteral)
                    tokens.Add(new Token(TokenType.String, remaining));
                else
                    tokens.Add(new Token(TokenType.Text, remaining));
            }
            
            return tokens;
        }

        static string ConvertToHtml(string code, bool showLineNumbers, string theme)
        {
            // Parse tokens
            var tokens = ParseTokens(code);
            
            // Escape HTML cho token text
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Type == TokenType.Text)
                {
                    tokens[i] = new Token(TokenType.Text, EscapeHtml(tokens[i].Value));
                }
            }
            
            // Kết hợp tokens thành HTML
            var htmlBuilder = new StringBuilder();
            
            if (showLineNumbers)
            {
                htmlBuilder.AppendLine(@"
                <style>
                    .code-container {
                        display: flex;
                        font-family: 'Consolas', 'Courier New', monospace;
                        font-size: 14px;
                        line-height: 1.5;
                    }
                    .line-numbers {
                        text-align: right;
                        padding-right: 15px;
                        margin-right: 15px;
                        border-right: 1px solid var(--border-color);
                        color: var(--line-number-color);
                        user-select: none;
                        background-color: var(--bg-secondary);
                    }
                    .code-content {
                        flex: 1;
                    }
                </style>
                <div class='code-container'>
                    <div class='line-numbers'>");
                
                // Thêm số dòng
                int lineCount = code.Split('\n').Length;
                for (int i = 1; i <= lineCount; i++)
                {
                    htmlBuilder.AppendLine($"{i}<br>");
                }
                
                htmlBuilder.AppendLine("</div><div class='code-content'><code>");
            }
            else
            {
                htmlBuilder.AppendLine("<pre><code>");
            }
            
            // Ghi các token
            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.Keyword:
                        htmlBuilder.Append($"<span class='keyword'>{token.Value}</span>");
                        break;
                    case TokenType.Preprocessor:
                        htmlBuilder.Append($"<span class='preprocessor'>{token.Value}</span>");
                        break;
                    case TokenType.Comment:
                        htmlBuilder.Append($"<span class='comment'>{token.Value}</span>");
                        break;
                    case TokenType.String:
                        htmlBuilder.Append($"<span class='string'>{token.Value}</span>");
                        break;
                    case TokenType.CharLiteral:
                        htmlBuilder.Append($"<span class='char'>{token.Value}</span>");
                        break;
                    case TokenType.Number:
                        htmlBuilder.Append($"<span class='number'>{token.Value}</span>");
                        break;
                    default:
                        htmlBuilder.Append(token.Value);
                        break;
                }
            }
            
            if (showLineNumbers)
            {
                htmlBuilder.AppendLine("</code></div></div>");
            }
            else
            {
                htmlBuilder.AppendLine("</code></pre>");
            }
            
            // CSS theme
            string themeCSS = theme == "dark" ? GetDarkTheme() : (theme == "light" ? GetLightTheme() : GetAutoTheme());
            
            // Tạo HTML hoàn chỉnh
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>C# Code - {DateTime.Now:yyyy-MM-dd HH:mm:ss}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Segoe UI', 'Consolas', 'Courier New', monospace;
            font-size: 14px;
            margin: 0;
            padding: 20px;
            min-height: 100vh;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: var(--bg-primary);
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            overflow: hidden;
        }}
        
        .header {{
            background: var(--header-bg);
            color: var(--header-text);
            padding: 15px 20px;
            border-bottom: 3px solid var(--accent-color);
        }}
        
        .header h1 {{
            margin: 0;
            font-size: 18px;
        }}
        
        .content {{
            padding: 20px;
            overflow-x: auto;
        }}
        
        pre, code {{
            font-family: 'Consolas', 'Courier New', monospace;
            font-size: 14px;
            line-height: 1.6;
            margin: 0;
        }}
        
        .keyword {{ color: var(--keyword-color); font-weight: bold; }}
        .preprocessor {{ color: var(--preprocessor-color); }}
        .comment {{ color: var(--comment-color); font-style: italic; }}
        .string {{ color: var(--string-color); }}
        .char {{ color: var(--char-color); }}
        .number {{ color: var(--number-color); }}
        
        {themeCSS}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📄 C# Source Code Highlighted</h1>
        </div>
        <div class='content'>
            {htmlBuilder}
        </div>
    </div>
</body>
</html>";
        }

        static string GetLightTheme()
        {
            return @"
        :root {
            --bg-primary: #ffffff;
            --bg-secondary: #f8f8f8;
            --header-bg: #2d3748;
            --header-text: #ffffff;
            --accent-color: #667eea;
            --border-color: #ddd;
            --line-number-color: #999;
            --keyword-color: #0000FF;
            --preprocessor-color: #6c6c6c;
            --comment-color: #008000;
            --string-color: #A31515;
            --char-color: #A31515;
            --number-color: #098658;
        }";
        }

        static string GetDarkTheme()
        {
            return @"
        :root {
            --bg-primary: #1e1e1e;
            --bg-secondary: #252526;
            --header-bg: #0d1117;
            --header-text: #c9d1d9;
            --accent-color: #58a6ff;
            --border-color: #404040;
            --line-number-color: #858585;
            --keyword-color: #569cd6;
            --preprocessor-color: #9cdcfe;
            --comment-color: #6a9955;
            --string-color: #ce9178;
            --char-color: #ce9178;
            --number-color: #b5cea8;
        }
        
        body {
            background: #0d1117;
        }";
        }

        static string GetAutoTheme()
        {
            return @"
        :root {
            --bg-primary: #ffffff;
            --bg-secondary: #f8f8f8;
            --header-bg: #2d3748;
            --header-text: #ffffff;
            --accent-color: #667eea;
            --border-color: #ddd;
            --line-number-color: #999;
            --keyword-color: #0000FF;
            --preprocessor-color: #6c6c6c;
            --comment-color: #008000;
            --string-color: #A31515;
            --char-color: #A31515;
            --number-color: #098658;
        }
        
        @media (prefers-color-scheme: dark) {
            :root {
                --bg-primary: #1e1e1e;
                --bg-secondary: #252526;
                --header-bg: #0d1117;
                --header-text: #c9d1d9;
                --accent-color: #58a6ff;
                --border-color: #404040;
                --line-number-color: #858585;
                --keyword-color: #569cd6;
                --preprocessor-color: #9cdcfe;
                --comment-color: #6a9955;
                --string-color: #ce9178;
                --char-color: #ce9178;
                --number-color: #b5cea8;
            }
            
            body {
                background: #0d1117;
            }
        }
        
        body {
            background: var(--bg-primary);
        }";
        }

        static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
#if NET472
            return HttpUtility.HtmlEncode(text);
#else
            return WebUtility.HtmlEncode(text);
#endif
        }
    }
}

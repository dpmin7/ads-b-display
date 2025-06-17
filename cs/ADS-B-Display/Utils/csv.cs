using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display
{
    public enum CsvState
    {
        Illegal,
        Normal,
        Quoted,
        Escaped,
        Comment,
        Stop,
        EOF
    }

    public class CsvContext
    {
        public string FileName { get; set; }
        public StreamReader Reader { get; set; }
        public char Delimiter { get; set; } = ',';
        public int NumFields { get; set; } = 0;
        public int RecordMax { get; set; } = int.MaxValue;
        public int RecordNum { get; set; } = 0;
        public int FieldNum { get; set; } = 0;
        public int LineSize { get; set; } = 1000;
        public CsvState State { get; set; } = CsvState.Illegal;

        public Func<CsvContext, string, bool> Callback { get; set; }
    }

    public class CsvParser
    {
        private CsvContext _ctx;
        private StringBuilder _buffer = new StringBuilder();

        public CsvParser(CsvContext context)
        {
            _ctx = context;
        }

        public int Parse()
        {
            if (!PrepareContext()) return 0;

            while (_ctx.RecordNum < _ctx.RecordMax) {
                if (!ParseRecord()) break;
            }

            if (_ctx.Reader != null)
                _ctx.Reader.Close();
            return _ctx.RecordNum;
        }

        private bool PrepareContext()
        {
            if (_ctx.Callback == null || string.IsNullOrWhiteSpace(_ctx.FileName))
                return false;

            if (_ctx.Delimiter == '#' || _ctx.Delimiter == '"' || _ctx.Delimiter == '\n')
                return false;

            _ctx.Reader = new StreamReader(_ctx.FileName);

            if (_ctx.NumFields == 0) {
                var line = _ctx.Reader.ReadLine();
                if (line == null) return false;
                _ctx.NumFields = line.Split(_ctx.Delimiter).Length;
                _ctx.Reader.BaseStream.Seek(0, SeekOrigin.Begin);
                _ctx.Reader.DiscardBufferedData();
            }

            return true;
        }

        private bool ParseRecord()
        {
            _ctx.FieldNum = 0;

            while (_ctx.FieldNum < _ctx.NumFields) {
                string field = GetNextField();
                if (field == null) return false;
                if (!_ctx.Callback(_ctx, field)) return false;
                _ctx.FieldNum++;
            }

            _ctx.RecordNum++;
            return true;
        }

        private string GetNextField()
        {
            _buffer.Clear();
            _ctx.State = CsvState.Normal;

            while (true) {
                int ch = _ctx.Reader.Read();
                if (ch == -1) {
                    _ctx.State = CsvState.EOF;
                    break;
                }

                char c = (char)ch;

                switch (_ctx.State) {
                    case CsvState.Normal:
                        if (c == _ctx.Delimiter)
                            return _buffer.ToString();
                        else if (c == '"')
                            _ctx.State = CsvState.Quoted;
                        else if (c == '\n')
                            return _buffer.ToString();
                        else if (c == '#' && _ctx.FieldNum == 0)
                            _ctx.State = CsvState.Comment;
                        else
                            _buffer.Append(c);
                        break;

                    case CsvState.Quoted:
                        if (c == '"')
                            _ctx.State = CsvState.Normal;
                        else if (c == '\n')
                            _buffer.Append(' ');
                        else if (c == '\\')
                            _ctx.State = CsvState.Escaped;
                        else
                            _buffer.Append(c);
                        break;

                    case CsvState.Escaped:
                        if (c == '"')
                            _buffer.Append('"');
                        _ctx.State = CsvState.Quoted;
                        break;

                    case CsvState.Comment:
                        if (c == '\n')
                            _ctx.State = CsvState.Normal;
                        break;

                    default:
                        break;
                }

                if (_ctx.State == CsvState.EOF)
                    return null;
            }

            return _buffer.ToString();
        }
    }
}

using System.Text;

namespace Nodis.Core.Streams;

public sealed class StringStream(string text, Encoding? encoding = null) :
    MemoryStream(encoding == null ? Encoding.Default.GetBytes(text) : encoding.GetBytes(text));
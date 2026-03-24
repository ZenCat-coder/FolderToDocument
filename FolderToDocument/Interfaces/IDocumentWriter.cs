using System.IO;
using System.Threading.Tasks;

namespace FolderToDocument.Interfaces;

/// <summary>文档写入接口</summary>
public interface IDocumentWriter
{
    /// <summary>写入字符串</summary>
    Task WriteAsync(TextWriter writer, string content);

    /// <summary>写入行</summary>
    Task WriteLineAsync(TextWriter writer, string content);
}
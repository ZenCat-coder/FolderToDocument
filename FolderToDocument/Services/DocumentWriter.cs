using System.IO;
using System.Threading.Tasks;
using FolderToDocument.Interfaces;

namespace FolderToDocument.Services;

/// <summary>文档写入实现</summary>
public class DocumentWriter : IDocumentWriter
{
    public async Task WriteAsync(TextWriter writer, string content)
        => await writer.WriteAsync(content);

    public async Task WriteLineAsync(TextWriter writer, string content)
        => await writer.WriteLineAsync(content);
}
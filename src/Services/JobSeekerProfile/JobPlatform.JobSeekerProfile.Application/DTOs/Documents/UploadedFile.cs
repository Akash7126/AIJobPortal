namespace JobPlatform.JobSeekerProfile.Application.DTOs.Documents;

/// <summary>The multipart body itself is read by the Api layer into a seekable stream before the command is built (foundation: Application has no HTTP types).</summary>
public sealed record UploadedFile(string FileName, string ContentType, long SizeBytes, Stream Content);

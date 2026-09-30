namespace JobPlatform.AiMatching.Application;

public sealed record ResumeContent(byte[] Bytes, string Format, string Sha256, long SizeBytes);

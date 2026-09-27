namespace JobPlatform.SharedKernel.ApiContracts.JobPosting;

/// <summary>
/// POST /internal/v1/reference-usage/check (service token, scope identity.internal). Implemented by the owners of references (BC-09 here;
/// BC-04/BC-10 may offer the same shape); called by BC-08 before it removes a reference-file entry (US-3.1.4-07, INV-04).
/// </summary>
/// <param name="Type">Reference file type in lower case: skills, jobs, trainings or datasets.</param>
/// <param name="Codes">Entry codes to check.</param>
public sealed record ReferenceUsageCheckRequest(string Type, IReadOnlyList<string> Codes);

/// <param name="InUse">The subset of the requested codes that an existing profile or posting still references.</param>
public sealed record ReferenceUsageCheckResponse(IReadOnlyList<string> InUse);

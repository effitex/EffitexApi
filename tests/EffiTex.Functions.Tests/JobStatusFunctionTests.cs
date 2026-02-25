using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using EffiTex.Functions.Models;
using EffiTex.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EffiTex.Functions.Tests;

public class JobStatusFunctionTests
{
    private readonly Mock<TableServiceClient> _tableServiceMock;
    private readonly Mock<TableClient> _tableClientMock;
    private readonly Mock<ILogger<JobStatusFunction>> _loggerMock;
    private readonly Mock<FunctionContext> _contextMock;
    private readonly JobStatusFunction _function;

    public JobStatusFunctionTests()
    {
        _tableServiceMock = new Mock<TableServiceClient>();
        _tableClientMock = new Mock<TableClient>();
        _loggerMock = new Mock<ILogger<JobStatusFunction>>();
        _contextMock = FunctionContextHelper.CreateMockContext();

        _tableServiceMock
            .Setup(x => x.GetTableClient("JobStatus"))
            .Returns(_tableClientMock.Object);

        _function = new JobStatusFunction(
            _tableServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Get_ExistingJob_ReturnsOkWithStatus()
    {
        var entity = new JobStatusEntity
        {
            PartitionKey = "Job",
            RowKey = "job123",
            JobId = "job123",
            Status = "completed",
            CompletedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Error = null
        };

        _tableClientMock
            .Setup(x => x.GetEntityAsync<JobStatusEntity>("Job", "job123", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var request = new MockHttpRequestData(_contextMock.Object, "", "");

        var response = await _function.Run(request, "job123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = ((MockHttpResponseData)response).ReadBody();
        var result = JsonDocument.Parse(body);
        result.RootElement.TryGetProperty("job_id", out var jobIdProp).Should().BeTrue();
        jobIdProp.GetString().Should().Be("job123");
        result.RootElement.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("completed");
    }

    [Fact]
    public async Task Get_NonExistentJob_Returns404()
    {
        _tableClientMock
            .Setup(x => x.GetEntityAsync<JobStatusEntity>("Job", "nonexistent", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var request = new MockHttpRequestData(_contextMock.Object, "", "");

        var response = await _function.Run(request, "nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = ((MockHttpResponseData)response).ReadBody();
        var result = JsonDocument.Parse(body);
        result.RootElement.TryGetProperty("error", out var errorProp).Should().BeTrue();
        errorProp.GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task Get_FailedJob_ReturnsStatusWithError()
    {
        var entity = new JobStatusEntity
        {
            PartitionKey = "Job",
            RowKey = "job456",
            JobId = "job456",
            Status = "failed",
            CompletedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Error = "Something went wrong"
        };

        _tableClientMock
            .Setup(x => x.GetEntityAsync<JobStatusEntity>("Job", "job456", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var request = new MockHttpRequestData(_contextMock.Object, "", "");

        var response = await _function.Run(request, "job456");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = ((MockHttpResponseData)response).ReadBody();
        var result = JsonDocument.Parse(body);
        result.RootElement.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("failed");
        result.RootElement.TryGetProperty("error", out var errorProp).Should().BeTrue();
        errorProp.GetString().Should().Be("Something went wrong");
    }
}

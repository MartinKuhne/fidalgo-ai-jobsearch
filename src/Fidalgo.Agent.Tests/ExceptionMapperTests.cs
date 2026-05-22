using Fidalgo.Agent.ErrorHandling;

namespace Fidalgo.Agent.Tests;

public class ExceptionMapperTests
{
    private readonly ExceptionMapper _mapper;

    public ExceptionMapperTests()
    {
        _mapper = new ExceptionMapper();
    }

    [Fact]
    public void MapToStatusCode_ShouldReturn400_ForValidationException()
    {
        var exception = new ValidationException(new Dictionary<string, string[]>());
        var statusCode = _mapper.MapToStatusCode(exception);
        Assert.Equal(400, statusCode);
    }

    [Fact]
    public void MapToStatusCode_ShouldReturn401_ForUnauthorizedException()
    {
        var exception = new UnauthorizedException("unauthorized");
        var statusCode = _mapper.MapToStatusCode(exception);
        Assert.Equal(401, statusCode);
    }

    [Fact]
    public void MapToStatusCode_ShouldReturn404_ForNotFoundException()
    {
        var exception = new NotFoundException("not found");
        var statusCode = _mapper.MapToStatusCode(exception);
        Assert.Equal(404, statusCode);
    }

    [Fact]
    public void MapToStatusCode_ShouldReturn500_ForGenericException()
    {
        var exception = new InvalidOperationException("generic");
        var statusCode = _mapper.MapToStatusCode(exception);
        Assert.Equal(500, statusCode);
    }

    [Fact]
    public void MapToStatusCode_ShouldReturn500_ForHttpRequestException()
    {
        var exception = new HttpRequestException("http error");
        var statusCode = _mapper.MapToStatusCode(exception);
        Assert.Equal(500, statusCode);
    }

    [Fact]
    public void MapToUserMessage_ShouldReturnValidationMessage_ForValidationException()
    {
        var exception = new ValidationException(new Dictionary<string, string[]> { { "email", new[] { "Invalid email" } } });
        var message = _mapper.MapToUserMessage(exception);
        Assert.Equal("Validation failed. Please check your input and try again.", message);
    }

    [Fact]
    public void MapToUserMessage_ShouldReturnGenericMessage_ForNonValidationError()
    {
        var exception = new HttpRequestException("network error");
        var message = _mapper.MapToUserMessage(exception);
        Assert.Equal("An unexpected error occurred. Please try again later.", message);
    }

    [Fact]
    public void IsValidationError_ShouldReturnTrue_ForValidationException()
    {
        var exception = new ValidationException(new Dictionary<string, string[]>());
        Assert.True(_mapper.IsValidationError(exception));
    }

    [Fact]
    public void IsValidationError_ShouldReturnFalse_ForOtherException()
    {
        var exception = new InvalidOperationException("error");
        Assert.False(_mapper.IsValidationError(exception));
    }

    [Fact]
    public void GetValidationErrors_ShouldReturnErrors_ForValidationException()
    {
        var errors = new Dictionary<string, string[]>
        {
            { "email", new[] { "Required" } },
            { "emailFormat", new[] { "Invalid format" } },
            { "name", new[] { "Too short" } }
        };
        var exception = new ValidationException(errors);
        var result = _mapper.GetValidationErrors(exception);

        Assert.Equal(3, result.Count);
        Assert.Contains("email", result);
        Assert.Contains("name", result);
        Assert.Equal("Required", result["email"]);
        Assert.Equal("Invalid format", result["emailFormat"]);
    }

    [Fact]
    public void GetValidationErrors_ShouldReturnEmptyDictionary_ForNonValidationError()
    {
        var exception = new InvalidOperationException("error");
        var result = _mapper.GetValidationErrors(exception);
        Assert.Empty(result);
    }

    [Fact]
    public void GetValidationErrors_ShouldReturnEmptyDictionary_ForNullValidationErrors()
    {
        var exception = new ValidationException(new Dictionary<string, string[]>());
        var result = _mapper.GetValidationErrors(exception);
        Assert.Empty(result);
    }
}

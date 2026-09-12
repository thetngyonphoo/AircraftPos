using AircraftPos.CalculatorLambda.Models;
using AircraftPos.CalculatorLambda.Services;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AircraftPos.CalculatorLambda;

public class Function
{
    private readonly IAmazonDynamoDB _dynamoDBClient;
    private readonly IAmazonS3 _s3Client;
    private readonly string _parsedRecordsTableName;
    private readonly string _posBucketName;
    private readonly string _calculationResultsTableName;
    private readonly IFlightCalculator _flightCalculator;

    public Function()
    {
        _dynamoDBClient = new AmazonDynamoDBClient();
        _s3Client = new AmazonS3Client();
        _flightCalculator = new FlightCalculator();

        _parsedRecordsTableName =
           Environment.GetEnvironmentVariable("PARSED_RECORDS_TABLE")
           ?? throw new InvalidOperationException(
               "PARSED_RECORDS_TABLE_NAME environment variable is not configured.");

        _posBucketName =
           Environment.GetEnvironmentVariable("POS_BUCKET_NAME")
           ?? throw new InvalidOperationException(
              "POS_BUCKET_NAME environment variable is not configured.");

        _calculationResultsTableName =
           Environment.GetEnvironmentVariable("CALCULATION_RESULTS_TABLE")
           ?? throw new InvalidOperationException(
              "CALCULATION_RESULTS_TABLE environment variable is not configured.");

    }

    public async Task FunctionHandler(SQSEvent sqsEvent,ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            context.Logger.LogInformation($"Received SQS message: {record.Body}");

            var message = JsonSerializer.Deserialize<CalculatorMessage>(
                record.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (message is null)
            {
                context.Logger.LogError("Failed to deserialize SQS message.");

                continue;
            }

            context.Logger.LogInformation($"FlightId: {message.FlightId}");

            context.Logger.LogInformation($"Timestamp: {message.Timestamp:O}");

            // Find parsed record in DynamoDB
            var parsedRecord = await GetParsedRecordAsync(message.FlightId,message.Timestamp);

            if (parsedRecord is null)
            {
                context.Logger.LogError(
                    $"Parsed record not found for " +
                    $"flightId={message.FlightId}, " +
                    $"timestamp={message.Timestamp:O}");

                continue;
            }

            // Get attachment path
            var attachment = parsedRecord["attachment"].S;

            context.Logger.LogInformation($"Parsed record found. Attachment: {attachment}");

            // Download parsed JSON from S3
            var parsedJson = await GetParsedJsonFromS3Async(attachment);

            context.Logger.LogInformation("Parsed JSON successfully retrieved from S3.");

            // Deserialize parsed POS report
            var posReport = JsonSerializer.Deserialize<ParsedPosReport>(parsedJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (posReport is null)
            {
                context.Logger.LogError(
                    $"Failed to deserialize parsed POS report " +
                    $"for flightId={message.FlightId}");

                continue;
            }

            var calculation = _flightCalculator.CalculateFlightResult(posReport);

            var resultAttachment = await SaveCalculationResultToS3Async(calculation);

            context.Logger.LogInformation($"Calculation result saved to S3: {resultAttachment}");

            await SaveCalculationResultRecordAsync(calculation,resultAttachment);

            context.Logger.LogInformation($"Calculation result record saved to DynamoDB: " + $"{calculation.FlightId}");
        }
    }

    private async Task<Dictionary<string, AttributeValue>?> GetParsedRecordAsync(string flightId, DateTime timestamp)
    {
        var request = new GetItemRequest
        {
            TableName = _parsedRecordsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["flightId"] = new AttributeValue
                {
                    S = flightId
                },
                ["timestamp"] = new AttributeValue
                {
                    S = timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
                }
            }
        };

        var response = await _dynamoDBClient.GetItemAsync(request);

        return response.Item.Count == 0
            ? null
            : response.Item;
    }

    private async Task<string> GetParsedJsonFromS3Async(string attachment)
    {
        var request = new GetObjectRequest
        {
            BucketName = _posBucketName,
            Key = attachment
        };

        using var response = await _s3Client.GetObjectAsync(request);
        using var reader = new StreamReader(response.ResponseStream);

        return await reader.ReadToEndAsync();
    }

    private async Task<string> SaveCalculationResultToS3Async(CalculationResult calculation)
    {
        var timestamp = calculation.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var key =$"results/{calculation.FlightId}-{timestamp}.json";

        var json = JsonSerializer.Serialize(
            calculation,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

        var request = new PutObjectRequest
        {
            BucketName = _posBucketName,
            Key = key,
            ContentBody = json,
            ContentType = "application/json"
        };

        await _s3Client.PutObjectAsync(request);

        return key;
    }

    private async Task SaveCalculationResultRecordAsync(CalculationResult calculation,string attachmentKey)
    {
        var timestamp = calculation.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var request = new PutItemRequest
        {
            TableName = _calculationResultsTableName,

            Item = new Dictionary<string, AttributeValue>
            {
                ["flightId"] = new AttributeValue
                {
                    S = calculation.FlightId
                },

                ["timestamp"] = new AttributeValue
                {
                    S = timestamp
                },

                ["attachment"] = new AttributeValue
                {
                    S = attachmentKey
                }
            }
        };

        await _dynamoDBClient.PutItemAsync(request);
    }
}

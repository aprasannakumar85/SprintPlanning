using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;

namespace SprintPlanning
{
    public class SprintPlanning
    {
        private static readonly string connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString", EnvironmentVariableTarget.Process).ToString();
        private static readonly CosmosClient _cosmosClient = new CosmosClient(connectionString);

        private readonly Database _database;
        private readonly Container _container;

        private const string DbName = "SprintPlanning";
        private const string DbContainerName = "SprintPlanningItems";

        public SprintPlanning()
        {
            _database = _cosmosClient.GetDatabase(DbName);
            _container = _database.GetContainer(DbContainerName);
        }

        [FunctionName("negotiate")]
        public static SignalRConnectionInfo Negotiate([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "sprintPlanningHub")] SignalRConnectionInfo connectionInfo, ILogger log)
        {
            log.LogInformation($"returning connection {connectionInfo.Url} {connectionInfo.AccessToken}");

            return connectionInfo;
        }

        [FunctionName("createSprintPlanTeamMember")]
        public async Task<IActionResult> CreateSprintPlanTeamMember([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "createSprintPlanTeamMember")] HttpRequest req,
           [SignalR(HubName = "sprintPlanningHub")] IAsyncCollector<SignalRMessage> signalRMessage, ILogger _logger)
        {
            IActionResult returnValue;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                var input = JsonConvert.DeserializeObject<SprintPlanningEntity>(requestBody);

                if(string.IsNullOrWhiteSpace(input.employer) || string.IsNullOrWhiteSpace(input.team) || string.IsNullOrWhiteSpace(input.teamMember) 
                    || string.IsNullOrWhiteSpace(input.sprintId))
                {
                    return new OkResult();
                }

                var id = $"{input.employer}{input.team}{input.sprintId}{input.teamMember}";

                var sprintPlanningEntity = new SprintPlanningEntity
                {
                    id = id,
                    employer = input.employer,
                    sprintId = input.sprintId,
                    team = input.team,
                    teamMember = input.teamMember
                };

                ItemResponse<SprintPlanningEntity> existingItem = null;

                try
                {
                    existingItem = await _container.ReadItemAsync<SprintPlanningEntity>(id, new PartitionKey(id));
                }
                catch
                {
                    var item = await _container.CreateItemAsync(sprintPlanningEntity);
                    _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                }

                if (existingItem?.Resource != null)
                {
                    var item = await _container.ReplaceItemAsync(sprintPlanningEntity, id, new PartitionKey(id));
                    _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                }

                await signalRMessage.AddAsync(new SignalRMessage
                {
                    Target = "sprintPlanningTeamData",
                    Arguments = new[]
                    {
                        sprintPlanningEntity
                    }
                });

                _logger.LogInformation("Item inserted");
                returnValue = new OkObjectResult(sprintPlanningEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }

        [FunctionName("createSprintPlan")]
        public async Task<IActionResult> CreateSprintPlan([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "createSprintPlan")] HttpRequest req,
            [SignalR(HubName = "sprintPlanningHub")] IAsyncCollector<SignalRMessage> signalRMessage, ILogger _logger)
        {
            IActionResult returnValue;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                var input = JsonConvert.DeserializeObject<SprintPlanningEntity>(requestBody);

                var id = $"{input.employer}{input.team}{input.sprintId}{input.teamMember}";

                if (string.IsNullOrWhiteSpace(input.employer) || string.IsNullOrWhiteSpace(input.team) || string.IsNullOrWhiteSpace(input.teamMember)
                    || string.IsNullOrWhiteSpace(input.sprintId))
                {
                    return new OkResult();
                }

                var sprintPlanningEntity = new SprintPlanningEntity
                {
                    id = id,
                    employer = input.employer,
                    sprintId = input.sprintId,
                    team = input.team,
                    points = input.points,
                    teamMember = input.teamMember
                };

                ItemResponse<SprintPlanningEntity> existingItem = null;

                try
                {
                    existingItem = await _container.ReadItemAsync<SprintPlanningEntity>(id, new PartitionKey(id));
                }
                catch
                {
                    var item = await _container.CreateItemAsync(sprintPlanningEntity);
                    _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                }

                if (existingItem?.Resource != null)
                {
                    var item = await _container.ReplaceItemAsync(sprintPlanningEntity, id, new PartitionKey(id));
                    _logger.LogInformation($"This query cost: {item.RequestCharge} RU/s");
                }

                await signalRMessage.AddAsync(new SignalRMessage
                {
                    Target = "sprintPlanningTeamData",
                    Arguments = new[]
                    {
                        sprintPlanningEntity
                    }
                });

                _logger.LogInformation("Item inserted");
                returnValue = new OkObjectResult(sprintPlanningEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }

        [FunctionName("getSprintPlanningData")]
        public static IActionResult GetSprintPlanningData([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getSprintPlanningData/{employer}/{team}/{sprintId}")] HttpRequest req,
            [CosmosDB(databaseName: DbName, collectionName: DbContainerName, ConnectionStringSetting = "CosmosDbConnectionString",
            SqlQuery ="SELECT * FROM c WHERE c.employer ={employer} AND c.team = {team} AND c.sprintId = {sprintId}")] IEnumerable<SprintPlanningEntity> sprintPlanningEntities,
            ILogger log, string employer, string team, string sprintId)
        {
            try
            {
                if (sprintPlanningEntities == null)
                {
                    log.LogInformation($"Could not find data");
                    return new NotFoundResult();
                }

                log.LogInformation("Found the data!");
                return new OkObjectResult(sprintPlanningEntities);
            }
            catch (Exception ex)
            {
                log.LogError($"Something went wrong. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}

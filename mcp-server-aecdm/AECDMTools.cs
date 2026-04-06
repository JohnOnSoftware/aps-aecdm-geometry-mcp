using ModelContextProtocol.Server;
using System.ComponentModel;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;


using Autodesk.Data.Enums;
using Autodesk.Data.DataModels;
using Autodesk.SDKManager;
using System.IO;
using Autodesk.DataManagement.Model;
using Autodesk.Data.Interface;
using Autodesk.Data.OpenAPI;
using System.Text;
using System.Numerics;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AECDMTools
{
    private const string BASE_URL = "https://developer.api.autodesk.com/aec/beta/graphql";


    public static async Task<object> Query(GraphQL.GraphQLRequest query, string? regionHeader = null)
    {
        var client = new GraphQLHttpClient(BASE_URL, new NewtonsoftJsonSerializer());
        client.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Global.AccessToken);
        if (!String.IsNullOrWhiteSpace(regionHeader))
            client.HttpClient.DefaultRequestHeaders.Add("region", regionHeader);
        var response = await client.SendQueryAsync<object>(query);

        if (response.Data == null) return response.Errors[0].Message;
        return response.Data;
    }

    [McpServerTool, Description("Get the ACC hubs from the user")]
    public static async Task<string> GetHubs()
    {
        var query = new GraphQL.GraphQLRequest
        {
            Query = @"
                query {
                    hubs {
                        pagination {
                            cursor
                        }
                        results {
                            id
                            name
							alternativeIdentifiers{
							  dataManagementAPIHubId
							}

                        }
                    }
                }",
        };

        object data = await Query(query);

        JObject jsonData = JObject.FromObject(data);
        JArray hubs = (JArray)jsonData.SelectToken("hubs.results");
        List<Hub> hubList = new List<Hub>();
        foreach (var hub in hubs.ToList())
        {
            try
            {
                Hub newHub = new Hub();
                newHub.id = hub.SelectToken("id").ToString();
                newHub.name = hub.SelectToken("name").ToString();
                newHub.dataManagementAPIHubId = hub.SelectToken("alternativeIdentifiers.dataManagementAPIHubId").ToString();
                hubList.Add(newHub);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string hubsString = hubList.Select(hub => hub.ToString()).Aggregate((a, b) => $"{a}, {b}");
        return hubsString;
    }

    [McpServerTool, Description("Get the ACC projects from one hub")]
    public static async Task<string> GetProjects([Description("Hub id, don't use dataManagementAPIHubId")] string hubId)
    {
        var query = new GraphQLRequest
        {
            Query = @"
			    query GetProjects ($hubId: ID!) {
			        projects (hubId: $hubId) {
                        pagination {
                           cursor
                        }
                        results {
                            id
                            name
							alternativeIdentifiers{
							  dataManagementAPIProjectId
							}
                        }
			        }
			    }",
            Variables = new
            {
                hubId = hubId
            }
        };

        object data = await Query(query);

        JObject jsonData = JObject.FromObject(data);
        JArray projects = (JArray)jsonData.SelectToken("projects.results");

        List<Project> projectList = new List<Project>();
        foreach (var project in projects.ToList())
        {
            try
            {
                var newProject = new Project();
                newProject.id = project.SelectToken("id").ToString();
                newProject.name = project.SelectToken("name").ToString();
                newProject.dataManagementAPIProjectId = project.SelectToken("alternativeIdentifiers.dataManagementAPIProjectId").ToString();
                projectList.Add(newProject);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string projectsString = projectList.Select(project => project.ToString()).Aggregate((a, b) => $"{a}, {b}");
        return projectsString;
    }

    [McpServerTool, Description("Get the Designs/Models/ElementGroups from one project")]
    public static async Task<string> GetElementGroupsByProject([Description("Project id, don't use dataManagementAPIProjectId")] string projectId)
    {
        var query = new GraphQLRequest
        {
            Query = @"
			    query GetElementGroupsByProject ($projectId: ID!) {
			        elementGroupsByProject(projectId: $projectId) {
			            results{
			                id
			                name
											alternativeIdentifiers{
												fileVersionUrn
											}
			            }
			        }
			    }",
            Variables = new
            {
                projectId = projectId
            }
        };
        object data = await Query(query);

        JObject jsonData = JObject.FromObject(data);
        JArray elementGroups = (JArray)jsonData.SelectToken("elementGroupsByProject.results");

        List<ElementGroup> elementGroupsList = new List<ElementGroup>();
        foreach (var elementGroup in elementGroups.ToList())
        {
            try
            {
                ElementGroup newElementGroup = new ElementGroup();
                newElementGroup.id = elementGroup.SelectToken("id").ToString();
                newElementGroup.name = elementGroup.SelectToken("name").ToString();
                newElementGroup.fileVersionUrn = elementGroup.SelectToken("alternativeIdentifiers.fileVersionUrn").ToString();
                elementGroupsList.Add(newElementGroup);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string elementGroupsString = elementGroupsList.Select(eg => $"name: {eg.name}, id: {eg.id}, fileVersionUrn: {eg.fileVersionUrn}").Aggregate((a, b) => $"{a}, {b}");
        return elementGroupsString;
    }

    [McpServerTool, Description("Get the Elements from the ElementGroup/Design using a category filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")]
    public static async Task<string> GetElementsByElementGroupWithCategoryFilter([Description("ElementGroup id, not the file version urn")] string elementGroupId, [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string category)
    {
        var query = new GraphQLRequest
        {
            Query = @"
			query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!) {
			  elementsByElementGroup(elementGroupId: $elementGroupId, pagination: {limit:500}, filter: {query:$filter}) {
			    results{
			      id
			      name
			      properties {
			        results {
			            name
			            value
			        }
			      }
			    }
			  }
			}",
            Variables = new
            {
                elementGroupId = elementGroupId,
                filter = $"'property.name.category'=='{category}' and 'property.name.Element Context'=='Instance'"
            }
        };
        object data = await Query(query);

        JObject jsonData = JObject.FromObject(data);
        JArray elements = (JArray)jsonData.SelectToken("elementsByElementGroup.results");

        List<LocalElement> elementsList = new List<LocalElement>();
        //Loop through elements 
        foreach (var element in elements.ToList())
        {
            try
            {
                LocalElement newElement = new LocalElement();
                newElement.id = element.SelectToken("id").ToString();
                newElement.name = element.SelectToken("name").ToString();
                newElement.properties = new List<Property>();
                JArray properties = (JArray)element.SelectToken("properties.results");
                foreach (JToken property in properties.ToList())
                {
                    try
                    {
                        newElement.properties.Add(new Property
                        {
                            name = property.SelectToken("name").ToString(),
                            value = property.SelectToken("value").ToString()
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                elementsList.Add(newElement);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        string elementsString = elementsList.Select(el => el.ToString()).Aggregate((a, b) => $"{a}; {b}");
        Console.WriteLine(elementsString);
        return elementsString;
    }


    [McpServerTool, Description("Export IFC file for elements from the ElementGroup/Design using multiple category filters")]
    public static async Task<string> ExportIfcForElementGroup(
        [Description("ElementGroup id, not the file version urn")] string elementGroupId,
        [Description("Array of category names to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string[] categories,
        [Description("File name of this exported IFC file.")] string? fileName = null)
    {
        string path = string.Empty;
        try
        {
            var elementGroup = new Autodesk.Data.DataModels.ElementGroup(Global.SDKClient);
            // Build filter dynamically based on categories parameter
            var categoryFilters = categories.Select(category =>
                ElementPropertyFilter.AllOf(
                    ElementPropertyFilter.Property("category", "==", category),
                    ElementPropertyFilter.Property("Element Context", "==", "Instance")
                )
            ).ToArray();

            // Create the combined filter using AnyOf for multiple categories
            var filter = categoryFilters.Length > 1
                ? ElementPropertyFilter.AnyOf(categoryFilters)
                : categoryFilters.FirstOrDefault();

            if (filter != null)
            {
                var egInfo = await TryResolveElementGroupInfoAsync(elementGroupId);
                if (egInfo == null)
                    throw new InvalidOperationException($"Element group not found for id: {elementGroupId}");
                await elementGroup.GetElementsAsync(egInfo, filter);
            }
            else
            {
                throw new ArgumentException("At least one category must be provided");
            }

            path = await elementGroup.ConvertToIfc(ifcFileId: fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nApplication failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            path = ex.Message;
        }

        return path;
    }


    [McpServerTool, Description("Find elements within specified categories that are spatially contained inside a given container element. Loads all instances in the element group via SDK, then filters by container Revit External ID and categories in code.")]
    public static async Task<string> FindElementsContainedWithin(
        [Description("Revit External ID of the container element used to resolve geometry and check containment")] string containerElementId,
        [Description("Array of element categories to search for contained elements. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string[] categories,
        [Description("Element group ID where to search for both container and contained elements")] string elementGroupId)
    {
        var resultBuilder = new StringBuilder();
        var containedElements = new List<ContainmentResult>();

        try
        {
            // Get container element geometry directly
            var containerElementGroup = new Autodesk.Data.DataModels.ElementGroup(Global.SDKClient);

            var egInfo = await TryResolveElementGroupInfoAsync(elementGroupId);
            if (egInfo == null)
                return $"Error: Element group not found for id: {elementGroupId}";

            var elementFilter = ElementPropertyFilter.AnyOf(
                ElementPropertyFilter.Property("External ID", "==", containerElementId));

            var elements = await containerElementGroup.GetElementsAsync(egInfo, elementFilter);
            var containerElementGeometry = await containerElementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

            var containerElement = containerElementGeometry.FirstOrDefault();
            var containerBoundingBox = CreateBoundingBoxFromElementGeometry(containerElement.Key, containerElement.Value);

            if (containerBoundingBox == null)
            {
                return "Error: Could not create bounding box for container element.";
            }

            resultBuilder.AppendLine("=== SPATIAL CONTAINMENT ANALYSIS ===");
            resultBuilder.AppendLine($"Container Element: {containerBoundingBox.ElementName} (ID: {containerElementId})");
            resultBuilder.AppendLine($"Container Bounding Box: ({containerBoundingBox.MinX:F2}, {containerBoundingBox.MinY:F2}, {containerBoundingBox.MinZ:F2}) to ({containerBoundingBox.MaxX:F2}, {containerBoundingBox.MaxY:F2}, {containerBoundingBox.MaxZ:F2})");
            resultBuilder.AppendLine($"Container Volume: {containerBoundingBox.Volume:F2} cubic units");
            resultBuilder.AppendLine($"Searching categories: {string.Join(", ", categories)}");
            resultBuilder.AppendLine();

            // Build filter for specified categories
            var categoryFilters = categories.Select(category =>
                ElementPropertyFilter.AllOf(
                    ElementPropertyFilter.Property("category", "==", category),
                    ElementPropertyFilter.Property("Element Context", "==", "Instance")

                )
            ).ToArray();

            var filter = categoryFilters.Length > 1
                ? ElementPropertyFilter.AnyOf(categoryFilters)
                : categoryFilters.FirstOrDefault();

            if (filter == null)
            {
                return "Error: At least one category must be provided.";
            }

            // Get elements from specified categories in the element group
            var categoryElementGroup = new Autodesk.Data.DataModels.ElementGroup(Global.SDKClient);
            await categoryElementGroup.GetElementsAsync(egInfo, filter);
            var categoryElementsGeometry = await categoryElementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

            resultBuilder.AppendLine($"Found {categoryElementsGeometry.Count} elements in specified categories to check for containment.");
            resultBuilder.AppendLine();

            // Debug: Log container bounds for troubleshooting
            Console.WriteLine($"DEBUG: Container bounds: X({containerBoundingBox.MinX:F3} to {containerBoundingBox.MaxX:F3}), Y({containerBoundingBox.MinY:F3} to {containerBoundingBox.MaxY:F3}), Z({containerBoundingBox.MinZ:F3} to {containerBoundingBox.MaxZ:F3})");

            // Check each category element for containment within the container
            foreach (var elementGeometry in categoryElementsGeometry)
            {
                var element = elementGeometry.Key;
                var geometries = elementGeometry.Value;

                // Skip the container element itself (compare AEC DM element id; container was resolved via Revit External ID)
                if (element.Id == containerElement.Key.Id)
                    continue;

                var elementBoundingBox = CreateBoundingBoxFromElementGeometry(element, geometries);
                if (elementBoundingBox == null)
                    continue;

                // Debug: Log element bounds for troubleshooting
                Console.WriteLine($"DEBUG: Checking element {element.Id} bounds: X({elementBoundingBox.MinX:F3} to {elementBoundingBox.MaxX:F3}), Y({elementBoundingBox.MinY:F3} to {elementBoundingBox.MaxY:F3}), Z({elementBoundingBox.MinZ:F3} to {elementBoundingBox.MaxZ:F3})");

                var containmentType = DetermineContainment(containerBoundingBox, elementBoundingBox);

                Console.WriteLine($"DEBUG: Element {element.Id} containment result: {containmentType}");

                if (containmentType != ContainmentType.Outside)
                {
                    var containmentResult = new ContainmentResult
                    {
                        ElementId = element.Id,
                        ElementName = elementBoundingBox.ElementName,
                        ContainmentType = containmentType,
                        ElementBoundingBox = elementBoundingBox,
                        Category = GetElementCategoryFromProperties(element)
                    };

                    containedElements.Add(containmentResult);
                }
            }

            // Generate results summary
            resultBuilder.AppendLine("=== CONTAINMENT RESULTS ===");

            if (!containedElements.Any())
            {
                resultBuilder.AppendLine("❌ No elements found within the container element.");
            }
            else
            {
                var fullyContained = containedElements.Where(e => e.ContainmentType == ContainmentType.FullyContained).ToList();
                var partiallyContained = containedElements.Where(e => e.ContainmentType == ContainmentType.PartiallyContained).ToList();

                resultBuilder.AppendLine($"✅ Found {containedElements.Count} contained elements:");
                resultBuilder.AppendLine($"   - Fully contained: {fullyContained.Count}");
                resultBuilder.AppendLine($"   - Partially contained: {partiallyContained.Count}");
                resultBuilder.AppendLine();

                // Group by category for better organization
                var groupedResults = containedElements.GroupBy(e => e.Category);

                foreach (var categoryGroup in groupedResults)
                {
                    resultBuilder.AppendLine($"📂 {categoryGroup.Key} ({categoryGroup.Count()} elements):");

                    foreach (var result in categoryGroup)
                    {
                        resultBuilder.AppendLine($"   {GetContainmentIcon(result.ContainmentType)} {result.ElementName} (ID: {result.ElementId})");
                        resultBuilder.AppendLine($"      External ID: {result.ExternalId}");
                        resultBuilder.AppendLine($"      Type: {result.ContainmentType}");
                        resultBuilder.AppendLine($"      Bounding Box: ({result.ElementBoundingBox.MinX:F2}, {result.ElementBoundingBox.MinY:F2}, {result.ElementBoundingBox.MinZ:F2}) to ({result.ElementBoundingBox.MaxX:F2}, {result.ElementBoundingBox.MaxY:F2}, {result.ElementBoundingBox.MaxZ:F2})");
                        resultBuilder.AppendLine($"      Volume: {result.ElementBoundingBox.Volume:F2} cubic units");
                        resultBuilder.AppendLine();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Application failed with error: {ex.Message}\nStack trace: {ex.StackTrace}";
            Console.WriteLine($"\n{errorMessage}");
            return $"Error during spatial containment analysis: {ex.Message}";
        }

        return resultBuilder.ToString();
    }

    [McpServerTool, Description("Find specific elements by Revit External ID inside a container. Requires element group id (design); loads all instances in the group, then filters to container and requested elements by External ID.")]
    public static async Task<string> FindSpecificElementsContainedWithin(
        [Description("Element group id for the design (not file version URN)")] string elementGroupId,
        [Description("Revit External ID of the container element used to resolve geometry and check containment")] string containerElementId,
        [Description("Revit External IDs of elements to check for containment within the container")] string[] elementIds)
    {
        var resultBuilder = new StringBuilder();
        var containedElements = new List<ContainmentResult>();

        try
        {
            // Check for duplicate element IDs
            var duplicateIds = elementIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateIds.Any())
            {
                return $"Error: Duplicate element IDs found: {string.Join(", ", duplicateIds)}. Each element ID should only appear once in the array.";
            }
            // Get container element geometry directly
            var containerElementGroup = new Autodesk.Data.DataModels.ElementGroup(Global.SDKClient);

            var egInfo = await TryResolveElementGroupInfoAsync(elementGroupId);
            if (egInfo == null)
                return $"Error: Element group not found for id: {elementGroupId}";

            var containerFilter = ElementPropertyFilter.AnyOf(
                ElementPropertyFilter.Property("External ID", "==", containerElementId));

            var containerElems = await containerElementGroup.GetElementsAsync(egInfo, containerFilter);
            var containerElementGeometry = await containerElementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

            var containerElement = containerElementGeometry.FirstOrDefault();
            var containerBoundingBox = CreateBoundingBoxFromElementGeometry(containerElement.Key, containerElement.Value);

            if (containerBoundingBox == null)
            {
                return "Error: Could not create bounding box for container element.";
            }

            resultBuilder.AppendLine("=== SPATIAL CONTAINMENT ANALYSIS (SPECIFIC ELEMENTS) ===");
            resultBuilder.AppendLine($"Container Element: {containerBoundingBox.ElementName} (Revit External ID: {containerElementId})");
            resultBuilder.AppendLine($"Container Bounding Box: ({containerBoundingBox.MinX:F2}, {containerBoundingBox.MinY:F2}, {containerBoundingBox.MinZ:F2}) to ({containerBoundingBox.MaxX:F2}, {containerBoundingBox.MaxY:F2}, {containerBoundingBox.MaxZ:F2})");
            resultBuilder.AppendLine($"Container Volume: {containerBoundingBox.Volume:F2} cubic units");
            resultBuilder.AppendLine($"Checking {elementIds.Length} specific elements (by Revit External ID) for containment");
            resultBuilder.AppendLine();

            // Debug: Log container bounds for troubleshooting
            Console.WriteLine($"DEBUG: Container bounds: X({containerBoundingBox.MinX:F3} to {containerBoundingBox.MaxX:F3}), Y({containerBoundingBox.MinY:F3} to {containerBoundingBox.MaxY:F3}), Z({containerBoundingBox.MinZ:F3} to {containerBoundingBox.MaxZ:F3})");

            var processedElementsCount = 0;
            var missingElements = new List<string>();

            // Iterate through each element ID individually to get mesh geometry and check containment
            foreach (var elementId in elementIds)
            {
                // Skip the container element itself
                if (elementId == containerElementId)
                {
                    resultBuilder.AppendLine($"⚠️ Skipping container element {elementId} from containment analysis");
                    continue;
                }

                try
                {
                    // Create a new element group for each individual element
                    var individualElementGroup = new Autodesk.Data.DataModels.ElementGroup(Global.SDKClient);
                    var elementFilter = ElementPropertyFilter.AnyOf(
                        ElementPropertyFilter.Property("External ID", "==", elementId));

                    var individualElems = await individualElementGroup.GetElementsAsync(egInfo, elementFilter);
                    var individualElementGeometry = await individualElementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

                    if (!individualElementGeometry.Any())
                    {
                        missingElements.Add(elementId);
                        resultBuilder.AppendLine($"⚠️ Warning: Element {elementId} not found or has no geometry");
                        continue;
                    }

                    // Process all geometry entries for this element to create a comprehensive bounding box
                    var element = individualElementGeometry.First().Key;
                    var allGeometries = new List<ElementGeometry>();

                    // Collect all geometries from all entries (an element can have multiple geometry entries)
                    foreach (var geometryEntry in individualElementGeometry)
                    {
                        allGeometries.AddRange(geometryEntry.Value);
                    }

                    var elementBoundingBox = CreateBoundingBoxFromElementGeometry(element, allGeometries);
                    if (elementBoundingBox == null)
                    {
                        resultBuilder.AppendLine($"⚠️ Warning: Could not create bounding box for element {elementId}");
                        continue;
                    }

                    // Debug: Log mesh count for this element
                    Console.WriteLine($"DEBUG: Element {elementId} has {allGeometries.Count} total mesh geometries");

                    // Debug: Log element bounds for troubleshooting
                    Console.WriteLine($"DEBUG: Checking element {elementId} bounds: X({elementBoundingBox.MinX:F3} to {elementBoundingBox.MaxX:F3}), Y({elementBoundingBox.MinY:F3} to {elementBoundingBox.MaxY:F3}), Z({elementBoundingBox.MinZ:F3} to {elementBoundingBox.MaxZ:F3})");

                    var containmentType = DetermineContainment(containerBoundingBox, elementBoundingBox);

                    Console.WriteLine($"DEBUG: Element {elementId} containment result: {containmentType}");

                    // Always add the result, regardless of containment type, for transparency
                    var containmentResult = new ContainmentResult
                    {
                        ElementId = elementId,
                        ElementName = elementBoundingBox.ElementName,
                        ContainmentType = containmentType,
                        ElementBoundingBox = elementBoundingBox,
                        Category = GetElementCategoryFromProperties(element)
                    };

                    containedElements.Add(containmentResult);
                    processedElementsCount++;
                }
                catch (Exception ex)
                {
                    resultBuilder.AppendLine($"⚠️ Error processing element {elementId}: {ex.Message}");
                    Console.WriteLine($"ERROR: Failed to process element {elementId}: {ex.Message}");
                }
            }

            resultBuilder.AppendLine($"Successfully processed {processedElementsCount} out of {elementIds.Length} elements.");
            resultBuilder.AppendLine();

            if (missingElements.Any())
            {
                resultBuilder.AppendLine("⚠️ The following element IDs were not found or had no geometry:");
                foreach (var missingId in missingElements)
                {
                    resultBuilder.AppendLine($"   - {missingId}");
                }
                resultBuilder.AppendLine();
            }

            // Generate results summary
            resultBuilder.AppendLine("=== CONTAINMENT RESULTS ===");

            var fullyContained = containedElements.Where(e => e.ContainmentType == ContainmentType.FullyContained).ToList();
            var partiallyContained = containedElements.Where(e => e.ContainmentType == ContainmentType.PartiallyContained).ToList();
            var outside = containedElements.Where(e => e.ContainmentType == ContainmentType.Outside).ToList();

            resultBuilder.AppendLine($"📊 Analysis Summary:");
            resultBuilder.AppendLine($"   - Total elements checked: {containedElements.Count}");
            resultBuilder.AppendLine($"   - Fully contained: {fullyContained.Count}");
            resultBuilder.AppendLine($"   - Partially contained: {partiallyContained.Count}");
            resultBuilder.AppendLine($"   - Outside container: {outside.Count}");
            resultBuilder.AppendLine();

            // Detailed results for each element
            resultBuilder.AppendLine("📋 Detailed Results:");
            foreach (var result in containedElements)
            {
                resultBuilder.AppendLine($"{GetContainmentIcon(result.ContainmentType)} {result.ElementName} (ID: {result.ElementId})");
                resultBuilder.AppendLine($"   External ID: {result.ExternalId}");
                resultBuilder.AppendLine($"   Category: {result.Category}");
                resultBuilder.AppendLine($"   Containment: {result.ContainmentType}");
                resultBuilder.AppendLine($"   Bounding Box: ({result.ElementBoundingBox.MinX:F2}, {result.ElementBoundingBox.MinY:F2}, {result.ElementBoundingBox.MinZ:F2}) to ({result.ElementBoundingBox.MaxX:F2}, {result.ElementBoundingBox.MaxY:F2}, {result.ElementBoundingBox.MaxZ:F2})");
                resultBuilder.AppendLine($"   Volume: {result.ElementBoundingBox.Volume:F2} cubic units");

                // Add percentage of containment for partially contained elements
                if (result.ContainmentType == ContainmentType.PartiallyContained)
                {
                    // Calculate intersection volume for partially contained elements
                    float intersectionX = Math.Min(result.ElementBoundingBox.MaxX, containerBoundingBox.MaxX) - Math.Max(result.ElementBoundingBox.MinX, containerBoundingBox.MinX);
                    float intersectionY = Math.Min(result.ElementBoundingBox.MaxY, containerBoundingBox.MaxY) - Math.Max(result.ElementBoundingBox.MinY, containerBoundingBox.MinY);
                    float intersectionZ = Math.Min(result.ElementBoundingBox.MaxZ, containerBoundingBox.MaxZ) - Math.Max(result.ElementBoundingBox.MinZ, containerBoundingBox.MinZ);
                    double intersectionVolume = Math.Max(0, intersectionX) * Math.Max(0, intersectionY) * Math.Max(0, intersectionZ);
                    double percentageContained = (intersectionVolume / result.ElementBoundingBox.Volume) * 100;
                    resultBuilder.AppendLine($"   Percentage contained: {percentageContained:F1}%");
                }

                resultBuilder.AppendLine();
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Application failed with error: {ex.Message}\nStack trace: {ex.StackTrace}";
            Console.WriteLine($"\n{errorMessage}");
            return $"Error during spatial containment analysis: {ex.Message}";
        }

        return resultBuilder.ToString();
    }



    /// <summary>
    /// Gets the element name from the element, with fallback to ID
    /// </summary>
    private static string GetElementName(Autodesk.Data.DataModels.Element element)
    {
        try
        {
            // Try to get a meaningful name from element properties or use the ID
            return !string.IsNullOrEmpty(element.Name) ? element.Name : $"Element_{element.Id}";
        }
        catch
        {
            return $"Element_{element.Id}";
        }
    }


    /// <summary>
    /// Looks up <see cref="ElementGroupInfo"/> for the given AEC DM element group id by scanning every hub, then every project in each hub, then element groups (US region).
    /// </summary>
    private static async Task<ElementGroupInfo?> TryResolveElementGroupInfoAsync(string elementGroupId)
    {
        if (string.IsNullOrWhiteSpace(elementGroupId) || Global.SDKClient == null)
            return null;

        var hubs = await Global.SDKClient.GetHubsAsync();
        if (hubs == null || hubs.Count == 0)
            return null;

        foreach (var hub in hubs)
        {
            var projects = await Global.SDKClient.GetProjectsAsync(hub, Autodesk.Data.Enums.Region.US);
            if (projects == null)
                continue;

            foreach (var project in projects)
            {
                var egInfos = await Global.SDKClient.GetElementGroupsAsync(project, Autodesk.Data.Enums.Region.US);
                var egInfo = egInfos?.Find(eg => eg.Id == elementGroupId);
                if (egInfo != null)
                    return egInfo;
            }
        }

        return null;
    }


    /// <summary>
    /// Creates a bounding box from element geometry by processing all mesh geometries
    /// </summary>
    private static ElementBoundingBox? CreateBoundingBoxFromElementGeometry(Autodesk.Data.DataModels.Element element, IEnumerable<ElementGeometry> geometries)
    {
        try
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool hasVertices = false;
            int meshCount = 0;
            int totalVertices = 0;
            
            foreach (var geometry in geometries)
            {
                if (geometry is Autodesk.Data.DataModels.MeshGeometry meshGeometry && meshGeometry.Mesh?.Vertices != null)
                {
                    meshCount++;
                    int verticesInThisMesh = meshGeometry.Mesh.Vertices.Count;
                    totalVertices += verticesInThisMesh;
                    
                    foreach (var vertex in meshGeometry.Mesh.Vertices)
                    {
                        hasVertices = true;
                        float x = (float)vertex.X;
                        float y = (float)vertex.Y;
                        float z = (float)vertex.Z;

                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        minZ = Math.Min(minZ, z);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                        maxZ = Math.Max(maxZ, z);
                    }
                    
                    Console.WriteLine($"DEBUG: Processed mesh {meshCount} for element {element.Id} with {verticesInThisMesh} vertices");
                }
            }

            Console.WriteLine($"DEBUG: Element {element.Id} total: {meshCount} meshes, {totalVertices} vertices");

            if (!hasVertices)
            {
                Console.WriteLine($"DEBUG: No vertices found for element {element.Id}");
                return null;
            }

            // Ensure minimum size for zero-dimension boxes
            if (maxX - minX < 0.001f) { minX -= 0.0005f; maxX += 0.0005f; }
            if (maxY - minY < 0.001f) { minY -= 0.0005f; maxY += 0.0005f; }
            if (maxZ - minZ < 0.001f) { minZ -= 0.0005f; maxZ += 0.0005f; }

            Console.WriteLine($"DEBUG: Created bounding box for element {element.Id}: ({minX:F3}, {minY:F3}, {minZ:F3}) to ({maxX:F3}, {maxY:F3}, {maxZ:F3})");

            return new ElementBoundingBox
            {
                ElementId = element.Id,
                ElementName = GetElementName(element),
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                MaxX = maxX,
                MaxY = maxY,
                MaxZ = maxZ
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating bounding box for element {element.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Determines the containment relationship between container and element
    /// </summary>
    private static ContainmentType DetermineContainment(ElementBoundingBox container, ElementBoundingBox element)
    {
        // Use a more generous tolerance for real-world architectural geometry
        const float tolerance = 0.01f;  // Increased from 0.001f

        // Check if element is completely inside container (with tolerance for floating point precision)
        bool fullyContained = element.MinX >= (container.MinX - tolerance) &&
                             element.MinY >= (container.MinY - tolerance) &&
                             element.MinZ >= (container.MinZ - tolerance) &&
                             element.MaxX <= (container.MaxX + tolerance) &&
                             element.MaxY <= (container.MaxY + tolerance) &&
                             element.MaxZ <= (container.MaxZ + tolerance);

        if (fullyContained)
        {
            return ContainmentType.FullyContained;
        }

        // Check if there's any meaningful overlap (partial containment)
        // For partial containment, we need actual overlap, not just touching
        bool overlapsX = element.MaxX > (container.MinX + tolerance) && element.MinX < (container.MaxX - tolerance);
        bool overlapsY = element.MaxY > (container.MinY + tolerance) && element.MinY < (container.MaxY - tolerance);
        bool overlapsZ = element.MaxZ > (container.MinZ + tolerance) && element.MinZ < (container.MaxZ - tolerance);

        if (overlapsX && overlapsY && overlapsZ)
        {
            // Additional check: calculate actual intersection volume to ensure meaningful overlap
            float intersectionX = Math.Min(element.MaxX, container.MaxX) - Math.Max(element.MinX, container.MinX);
            float intersectionY = Math.Min(element.MaxY, container.MaxY) - Math.Max(element.MinY, container.MinY);
            float intersectionZ = Math.Min(element.MaxZ, container.MaxZ) - Math.Max(element.MinZ, container.MinZ);
            
            // Only consider it as containment if intersection volume is meaningful
            if (intersectionX > tolerance && intersectionY > tolerance && intersectionZ > tolerance)
            {
                double intersectionVolume = intersectionX * intersectionY * intersectionZ;
                double elementVolume = element.Volume;
                
                // If more than 1% of the element's volume is within the container, consider it contained
                if (intersectionVolume > (elementVolume * 0.01))
                {
                    return ContainmentType.PartiallyContained;
                }
            }
        }

        return ContainmentType.Outside;
    }

    /// <summary>
    /// Gets the category of an element from its properties
    /// </summary>
    private static string GetElementCategoryFromProperties(Autodesk.Data.DataModels.Element element)
    {
        try
        {
            // For now, just use element name/type for category inference
            // TODO: Fix Properties access when API documentation is available
            
            // Fallback: try to infer category from element name or type
            var elementName = element.Name?.ToLower() ?? "";
            
            if (elementName.Contains("wall")) return "Walls";
            if (elementName.Contains("door")) return "Doors";
            if (elementName.Contains("window")) return "Windows";
            if (elementName.Contains("floor")) return "Floors";
            if (elementName.Contains("ceiling")) return "Ceilings";
            if (elementName.Contains("roof")) return "Roofs";
            if (elementName.Contains("furniture")) return "Furniture";
            if (elementName.Contains("electrical")) return "Electrical Equipment";
            if (elementName.Contains("structural") && elementName.Contains("framing")) return "Structural Framing";
            if (elementName.Contains("structural") && elementName.Contains("column")) return "Structural Columns";
            if (elementName.Contains("rebar")) return "Structural Rebar";
            
            return "Unknown Category";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting element category: {ex.Message}");
            return "Unknown Category";
        }
    }


    /// <summary>
    /// Gets appropriate icon for containment type
    /// </summary>
    private static string GetContainmentIcon(ContainmentType containmentType)
    {
        return containmentType switch
        {
            ContainmentType.FullyContained => "🟢",
            ContainmentType.PartiallyContained => "🟡",
            ContainmentType.Outside => "🔴",
            _ => "⚪"
        };
    }

}

/// <summary>
/// Represents a bounding box for an element used in clash detection analysis
/// </summary>
internal class ElementBoundingBox
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }

    public double Volume => Math.Abs((MaxX - MinX) * (MaxY - MinY) * (MaxZ - MinZ));
    
    public Vector3 Center => new Vector3((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);
}

/// <summary>
/// Represents the types of spatial containment relationships
/// </summary>
internal enum ContainmentType
{
    Outside,
    PartiallyContained,
    FullyContained
}

/// <summary>
/// Represents the result of a containment analysis for an element
/// </summary>
internal class ContainmentResult
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public ContainmentType ContainmentType { get; set; }
    public ElementBoundingBox ElementBoundingBox { get; set; } = new ElementBoundingBox();
    public string Category { get; set; } = string.Empty;
}



internal class ElementGroup
{
	internal string id { get; set; } = string.Empty;
	internal string name { get; set; } = string.Empty;
	internal string fileVersionUrn { get; set; } = string.Empty;

	public override string ToString()
	{
		return $"id: {id}, name: {name}, fileVersionUrn: {fileVersionUrn}";
	}
}


internal class Hub
{
   internal string id { get; set; } = string.Empty;
    internal string name { get; set; } = string.Empty;
    internal string dataManagementAPIHubId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"hubId: {id}, hubName: {name}, dataManagementAPIHubId: {dataManagementAPIHubId}";
    }
}

internal class Project
{
    internal string id { get; set; } = string.Empty;
    internal string name { get; set; } = string.Empty;
    internal string dataManagementAPIProjectId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"projectId: {id}, projectName: {name}, dataManagementAPIProjectId: {dataManagementAPIProjectId}";
    }
}	




internal class LocalElement
{
	internal string id { get; set; } = string.Empty;
	internal string name { get; set; } = string.Empty;
	internal List<Property> properties { get; set; } = new List<Property>();

	public override string ToString()
	{
		var externalIdProp = properties.Find(prop => prop.name == "External ID");
		return $"id: {id}, name: {name}, external id: {externalIdProp?.value ?? "N/A"} ";
	}
}

internal class Property
{
	internal string name { get; set; } = string.Empty;
	internal string value { get; set; } = string.Empty;
	public override string ToString()
	{
		return $"name: {name}, value: {value}";
	}
}

![Platforms](https://img.shields.io/badge/platform-Windows-lightgray.svg)
![.NET](https://img.shields.io/badge/.NET%20-8-blue.svg)
[![License](http://img.shields.io/:license-MIT-blue.svg)](http://opensource.org/licenses/MIT)

[![oAuth2](https://img.shields.io/badge/oAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![Data-SDK](https://img.shields.io/badge/Data%20SDK-beta-green.svg)](http://developer.autodesk.com/)
[![AEC-Data-Model](https://img.shields.io/badge/AEC%20Data%20Model-v1-green.svg)](http://developer.autodesk.com/)


# aps-aecdm-mcp-dotnet
.NET MCP Server to connect with Autodesk Assistant or Claude Desktop, AEC Data Model API including the Geometry feature(beta) and the Viewer.


## Introduction
This project started as an experiment with the [Model Context Protocol](https://modelcontextprotocol.io/introduction) during an [Autodesk Platform Accelerator](https://aps.autodesk.com/accelerator-program). 

It continues as a comprehensive Model Context Protocol (MCP) local server implementation that enables AI Agency to interact with APS **AEC Data Model** with either **stdio** or **Streamable HTTP** communication as follow:
- Claude Desktop by **stdio** communication
- Autodesk Assistant by **Streamable HTTP** communication

The MCP server provides natural language access models from Autodesk Construction Cloud data, spatial analysis, and 3D visualization capabilities.

## Features

- 🔐 **Authentication** - OAuth token management for APS API access
- 🏗️ **BIM Navigation** - Browse hubs, projects, and element groups
- 🔍 **Element Querying** - Filter and retrieve building elements by category
- 📤 **IFC Export** - Export filtered elements to Industry Foundation Classes (IFC) format
- 📦 **Spatial Containment** - Find elements spatially contained within other elements
- 📁 **File Upload** - Upload files to Autodesk Docs (ACC/BIM 360)
- 👁️ **3D Visualization** - Render and highlight elements in Autodesk Viewer
- 🔌 **Dual Transport** - STDIO mode and Streamable HTTP mode

## Prerequisites

### Software Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- [Claude Desktop](https://claude.ai/download) (for local AI assistant integration)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) 

### Autodesk Platform Services

- An APS account (free at [aps.autodesk.com](https://aps.autodesk.com))
- An APS application with:
  - **App Type**: Single Page Application is recommended(for PKCE authentication)
  - **Callback URL**: `http://localhost:8080/api/auth/callback/`
  - **APIs Enabled**: Data Management API, AEC Data Model API, Model Derivative API
- Access to ACC (Autodesk Construction Cloud) projects

### AEC Data Model Geometry(beta)
- It's required to participate in the [AEC Data Model Public Beta Program](https://feedback.autodesk.com/) to access Geometry feature, follow the instructions, download the DataSDK, you can refer to the [aps-aecdm-data-sdk-code-sample
](https://github.com/autodesk-platform-services/aps-aecdm-data-sdk-code-sample) if you are interested in the feature.

### Autodesk Assistant
- Get your access to Autodesk Assistant staging version

### Provisioning

- **Important**: You must [provision your APS app in your ACC hub](https://get-started.aps.autodesk.com/#provision-access-in-other-products) to access project data.


## Installation

```bash
git clone https://github.com/JohnOnSoftware/aps-aecdm-mcp-dotnet.git
cd aps-aecdm-mcp-dotnet
```

### Build

From the repo root:

```bash
cd mcp-server-aecdm
dotnet build
```

**Note:** The packages `Autodesk.Data` and `Autodesk.Data.GeometryDefinitions` are part of the [AEC Data Model Public Beta](https://feedback.autodesk.com/). If restore fails with "Unable to find package Autodesk.Data", join the beta, install the Data SDK as described in the beta program, and add the required NuGet source (or local packages) to `nuget.config`. Until then, GraphQL-only features (hubs, projects, element lists) can still work if you reference a build that has those packages available.

## Setup

### 1. Set Environment Variables

You need to configure your APS credentials as environment variables, or you can set these in `Properties/launchSettings.json`:

```json
{
  "profiles": {
    "mcp-server-aecdm": {
      "commandName": "Project",
      "environmentVariables": {
        "CLIENT_ID": "your_aps_pkce_client_id",
        "CALLBACK_URL": "http://localhost:8080/api/auth/callback/"
      }
    }
  }
}
```

### 2. Running with AI Agent
#### Cursor

- This repo includes a Cursor MCP config in `.cursor/mcp.json`. After building the project:
  1. Set your APS `CLIENT_ID` in `.cursor/mcp.json` under `mcpServers.aecdm.env`, or set the `CLIENT_ID` environment variable on your system.
  2. Restart Cursor (MCP servers load at startup).
  3. Enable the **aecdm** server in Cursor: **Settings → Tools & MCP** (or use the MCP panel).
- If you use a different workspace root, ensure the `args` path to `mcp-server-aecdm.csproj` is correct, or use an absolute path.

#### Claude Desktop

- To connect this MCP server with Claude Desktop, add the following to your `claude_desktop_config.json` file:
  ```json
  {
    "mcpServers": {
      "aecdm": {
        "command": "dotnet",
        "args": [
          "run",
          "--project",
          "C:\\path-to-your-folder\\...mcp-server-aecdm.csproj",
          "--no-build"
        ]
      }
    }
  }
  ```
- Build the project with `dotnet build` 
- Run Claude Desktop and enable the MCP server **aecdm**
- Play with it.

####  Autodesk Assistant(Staging, Autodesk Internal Only)
- Build the project with `dotnet build`
- Run the App with `dotnet run --http` 
- Open the browser without CORS, for Chrome please refer [here](https://alfilatov.com/posts/run-chrome-without-cors/#google_vignette)
- Test in the browser by `http://127.0.0.1:4000/mcp/health`, you should get the status of this MCP server.
- Run Autodesk Assistant if everything is good, enable MCP.
- Play with it.


## Workflow Scenario from AI agent
- Please find my design "Sample.rvt" from ACC project "Sample Project”, and render it in the viewer.
- Show me all the stairs in the design and highlight them.
- Now list me all the rooms from this design.
- Please check last 5 stairs and tell me which are inside room "Stair S1”.
- Highlight the contained stairs in the viewer.
- Please export all the Doors from this design to IFC file with file name “DoorsSample”
- Upload this IFC file to Autodesk Docs, under same project. 

## Troubleshooting
- Can't find my hub -- **Solution**: Ensure your APS app is [provisioned in your ACC hub](https://get-started.aps.autodesk.com/#provision-access-in-other-products). This must be done by an ACC administrator.
- Code changes not reflected in Claude -- **Solution**: Completely quit Claude Desktop (check Task Manager/Activity Monitor), Rebuild your project: `dotnet build`, Restart Claude Desktop
- Port conflicts (8081/8082), Viewer tools fail if ports are already in use --**Solution**: Close applications using these ports or modify port numbers in `ViewerTool.cs`.
- Element IDs vs File Version URNs, Some tools require element group IDs, not file version URNs -- **Solution**: Use the ID from `GetElementGroupsByProject`, not the `fileVersionUrn` field.

## Video Tutorial

📺 **[Watch the Demo Video](https://youtu.be/GlCYJQfUFWU)** - See the MCP server in action with Claude Desktop.

## License

This sample is licensed under the terms of the [MIT License](LICENSE). Please see the [LICENSE](LICENSE) file for full details.

## Author

- **João Martins** - [LinkedIn](https://linkedin.com/in/jpornelas)
- **Zhong Wu** - [LinkedIn](https://linkedin.com/in/johnonsoftware)

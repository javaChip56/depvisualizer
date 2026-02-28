# Dependency Visualizer

ASP.NET Core Razor Pages app for modeling architecture dependencies by project, with role-based access, node/relationship management, and interactive graph preview/export.

## Tech Stack
- .NET 9 Razor Pages
- In-memory repositories (runtime data)
- Cytoscape.js for graph rendering
- Tom Select for searchable dropdowns
- Bootstrap 5 UI

## Current Functionality

### Authentication and Users
- Cookie-based login/logout
- Default seeded admin account from `appsettings.json`:
  - `AuthDefaults:AdminUsername`
  - `AuthDefaults:AdminPassword`
- Admin can create users with a default password
- New users are forced to change password on first login

### Admin Features
- Manage users:
  - create users
  - assign global Admin role
- Manage node types:
  - create/update/delete node types
  - assign Cytoscape shape
  - classify type as:
    - `Regular`
    - `Compound` (grouping type)
- Admin menu has sub-items:
  - `Manage Users`
  - `Manage Node Types`

### Projects and Membership
- Users can create projects (`name`, `description`)
- Project membership roles:
  - `Maintainer`
  - `Contributor`
- Maintainer can:
  - edit project details
  - delete project
  - add/update members and assign role
- Contributor can:
  - access member projects
  - manage nodes and relationships
- Users only see projects where they are members
- Project audit log is available per project (`Projects -> Audit`)

### Nodes
- Create/edit/delete nodes under a selected project
- Fields:
  - Name (unique per project, case-insensitive)
  - Type (from admin-managed node types)
  - Description
  - Parent compound node (optional)
  - Line color (`#RRGGBB`)
  - Fill color (`#RRGGBB`)
- Validation:
  - duplicate node names blocked within same project
  - parent must be compound type
  - parent cycles blocked
- Delete safeguards:
  - node delete is blocked if it still has relationships
  - compound node delete is blocked if it still has child nodes
- Duplicate node action in list:
  - creates `Name (Copy)`, `Name (Copy 2)`, etc.
- Delete actions require user confirmation

### Relationships
- Create/edit/delete relationships under selected project
- Prevents:
  - self-dependency
  - duplicate relationships
- Delete actions require user confirmation

### Preview
- Interactive Cytoscape graph preview per project
- Compound hierarchy rendering via parent-child nodes
- Node visuals include:
  - shape (from node type)
  - line color and fill color (from node)
  - regular nodes auto-expand horizontally to fit labels
- Layout behavior:
  - First load uses auto layout tuned for non-overlap and compact compound hierarchies
  - Node drag/layout changes are persisted
- Role-aware layout persistence:
  - Maintainer saves shared project layout (visible to all members)
  - Contributor saves personal layout override
  - Contributor can `Reset Layout` to maintainer layout
- Toolbar actions:
  - Recenter
  - Export PNG
  - Export JPG
  - Export SVG
  - Contributor-only Reset Layout

## Data Storage Notes
- Runtime entities are in-memory:
  - users
  - projects/memberships
  - nodes
  - relationships
  - layout positions
  - project audit entries
- App restart clears in-memory runtime data.
- Node type definitions are persisted to JSON files:
  - `node-types.json`
  - `node-shapes.json` (kept in sync for compatibility)

## Configuration

### `appsettings.json`
```json
{
  "AuthDefaults": {
    "AdminUsername": "admin",
    "AdminPassword": "redacted"
  }
}
```

## Run Locally

### Prerequisites
- .NET 9 SDK

### Commands
```powershell
cd d:\Research\depvisualizer
dotnet restore
dotnet run
```

Default dev URLs (from launch settings):
- `http://localhost:5153`
- `https://localhost:7224`

## Typical Usage Flow
1. Login as admin.
2. Create users.
3. Admin configures node types/shapes in `Admin -> Manage Node Types`.
4. Create a project.
5. Maintainer adds contributors/maintainers to project.
6. Create nodes and relationships under that project.
7. Open Preview, arrange graph, save layout automatically.
8. Contributors can reset to maintainer layout if needed.
9. Review project changes in `Projects -> Audit`.
10. Export graph as image if needed.

## Audit Logging
- Audit entries are recorded per project.
- Tracked events currently include:
  - node create/update/duplicate/delete
  - relationship create/update/delete
- Each entry captures:
  - timestamp (UTC)
  - user
  - action
  - entity type
  - details

## External Client-side Dependencies (CDN)
- Cytoscape.js
- cytoscape-svg plugin
- Tom Select

If CDN access is blocked in your environment, preview/export or enhanced dropdown UX may be limited until these assets are served locally.

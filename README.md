# Dependency Visualizer

ASP.NET Core Razor Pages app for modeling architecture dependencies by parent Project and scoped Sub Projects, with role-based access, node/relationship management, and interactive graph preview/export.

## Tech Stack
- .NET 9 Razor Pages
- In-memory repositories (runtime data)
- Cytoscape.js for graph rendering
- Tom Select for searchable dropdowns
- Tabulator for nested node/project views
- Bootstrap 5 UI

## Current Functionality

### Authentication and Users
- Cookie-based login/logout
- Default seeded admin account from `appsettings.json`:
  - `AuthDefaults:AdminUsername`
  - `AuthDefaults:AdminPassword`
- Admin can create users with a default password
- New users are forced to change password on first login
- Admin can suspend/unsuspend users from `Admin -> Manage Users`
- Suspended users cannot login, and active sessions are invalidated by middleware
- Safety guard: app prevents suspending the last active admin

### Admin Features
- Manage users:
  - create users
  - assign global Admin role
  - suspend/unsuspend users
- Manage node types:
  - create/update/delete node types
  - assign Cytoscape shape
  - classify type as:
    - `Regular`
    - `Compound` (grouping type)
- Admin menu sub-items:
  - `Manage Users`
  - `Manage Node Types`

## Project Model

### Parent/Child Structure
- `Project` is a parent container.
- Each `Project` can have multiple `Sub Projects`.
- Nodes, relationships, and preview layouts are attached to a `Sub Project` (not directly to Project).

### Projects Page UX
- Unified add section: `Add Project / Sub Project`.
  - Choose `Project` or `Sub Project` in one form.
  - When `Sub Project` is selected, a parent project is required.
- `My Projects` list is nested:
  - Parent row = Project
  - Child rows = Sub Projects under that Project
- Manage sections are exclusive:
  - `Manage Project` shows Project details section
  - `Manage Sub Project` hides Project details and shows Sub Project details

## Membership and Permissions

### Project Level
- Valid Project-level member role is `Maintainer` only (no Project-level Contributor assignment).
- Project Maintainer can:
  - edit Project details
  - delete Project
  - add/remove Project members (members are always Maintainers)
  - create/delete Sub Projects
- Users can still see a Project in `My Projects` via Sub Project membership, even without direct Project membership.

### Sub Project Level
- Sub Project roles:
  - `Maintainer`
  - `Contributor`
- Sub Project Maintainer can:
  - edit Sub Project details
  - add/remove Sub Project members
  - manage nodes/relationships/preview in that Sub Project
- Sub Project Contributor can:
  - edit Sub Project details
  - manage nodes/relationships/preview in that Sub Project
  - cannot add/remove Sub Project members

### Role Scope and Precedence
- Access scope is by Sub Project membership.
- A user only sees/manages Sub Projects they can access.
- Effective Sub Project permissions use this precedence:
  1. Direct Sub Project role assignment (if present)
  2. Fallback to inherited Project-level Maintainer rights
- Implication:
  - If user is Project Maintainer but explicitly assigned `Contributor` on a Sub Project, they are treated as Contributor in that Sub Project (cannot manage members there).

## Nodes
- Create/edit/delete nodes under a selected Sub Project
- Fields:
  - Name (unique per sub project, case-insensitive)
  - Type (from admin-managed node types)
  - Description
  - Parent compound node (optional)
  - Line color (`#RRGGBB`)
  - Fill color (`#RRGGBB`)
- Validation:
  - duplicate node names blocked within same Sub Project
  - parent must be compound type
  - parent cycles blocked
- Delete safeguards:
  - node delete is blocked if it still has relationships
  - compound node delete is blocked if it still has child nodes
- Duplicate node action in list:
  - creates `Name (Copy)`, `Name (Copy 2)`, etc.
- Delete actions require confirmation
- Existing Nodes list uses Tabulator tree table to display nested parent/child structure

## Relationships
- Create/edit/delete relationships under selected Sub Project
- Prevents:
  - self-dependency
  - duplicate relationships
- Delete actions require confirmation

## Preview
- Interactive Cytoscape graph preview per Sub Project
- Compound hierarchy rendering via parent-child nodes
- Relationship lines use `taxi` routing (bendy/orthogonal style)
- Node visuals include:
  - shape (from node type)
  - line color and fill color (from node)
  - regular nodes auto-expand horizontally to fit labels
- Layout behavior:
  - First load uses auto layout tuned for non-overlap and compact compound hierarchies
  - Node drag/layout changes are persisted
- Role-aware layout persistence (per Sub Project):
  - Maintainer saves shared Sub Project layout (visible to members)
  - Contributor saves personal layout override
  - Contributor can `Reset Layout` to maintainer layout
- Toolbar actions:
  - Recenter
  - Export PNG
  - Export JPG
  - Export SVG
  - Contributor-only Reset Layout

## Audit Logging
- Audit entries are recorded per Project (including Sub Project-related events)
- Tracked events include:
  - node create/update/duplicate/delete
  - relationship create/update/delete
  - project member add/remove
  - sub project create/update/delete
  - sub project member add/update/remove
- Each entry captures:
  - timestamp (UTC)
  - user
  - action
  - entity type
  - details

## Data Storage Notes
- Runtime entities are in-memory:
  - users
  - projects/memberships
  - sub projects/memberships
  - nodes
  - relationships
  - layout positions
  - project audit entries
- App restart clears in-memory runtime data
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
2. Create users and configure node types.
3. Create a parent Project.
4. Add Sub Projects under the Project.
5. Add Project members (Maintainer-only role at Project level).
6. Add Sub Project members and assign Maintainer/Contributor.
7. Create nodes and relationships within a Sub Project.
8. Open Preview, arrange graph, and save layout.
9. Review changes in `Projects -> Audit`.
10. Export graph image if needed.

## External Client-side Dependencies (CDN)
- Cytoscape.js
- cytoscape-svg plugin
- Tom Select
- Tabulator

If CDN access is blocked in your environment, preview/export or enhanced dropdown UX may be limited until these assets are served locally.

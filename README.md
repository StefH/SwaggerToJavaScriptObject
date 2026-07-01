# SwaggerToJavaScriptObject

A Blazor WebAssembly app that converts Swagger / OpenAPI definitions to JavaScript object declarations.

## 🚀 Live Demo

[https://stefh.github.io/SwaggerToJavaScriptObject/](https://stefh.github.io/SwaggerToJavaScriptObject/)

## Features

1. **Input** — Paste your Swagger 2.0 or OpenAPI 3.0 JSON document into the text area.
2. **Analyze** — Click *Analyze* to extract all type definitions (`definitions` in Swagger 2.0 or `components.schemas` in OpenAPI 3.0).
3. **Select types** — A list of checkboxes lets you select or deselect the types you want to generate. Use *Select All* / *Deselect All* for convenience.
4. **Generate** — Click *Generate* to bundle and dereference the selected types and produce JavaScript `const` declarations, e.g.:

``` javascript
const Pet = {
  "type": "object",
  "required": [
    "name",
    "photoUrls"
  ],
  "properties": {
    "id": {
      "type": "integer",
      "format": "int64"
    },
    "category": {
      "type": "object",
      "properties": {
        "id": {
          "type": "integer",
          "format": "int64"
        },
        "name": {
          "type": "string"
        }
      },
      "xml": {
        "name": "Category"
      }
    },
    "name": {
      "type": "string",
      "example": "doggie"
    },
    "photoUrls": {
      "type": "array",
      "xml": {
        "wrapped": true
      },
      "items": {
        "type": "string",
        "xml": {
          "name": "photoUrl"
        }
      }
    },
    "tags": {
      "type": "array",
      "xml": {
        "wrapped": true
      },
      "items": {
        "type": "object",
        "properties": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "name": {
            "type": "string"
          }
        },
        "xml": {
          "name": "Tag"
        }
      }
    },
    "status": {
      "type": "string",
      "description": "pet status in the store",
      "enum": [
        "available",
        "pending",
        "sold"
      ]
    }
  },
  "xml": {
    "name": "Pet"
  }
};
```

5. **Copy to clipboard** — Copy the generated JavaScript with one click.

## Tech stack

- [Blazor WebAssembly](https://docs.microsoft.com/en-us/aspnet/core/blazor/) (.NET 10)
- [Bootstrap 5](https://getbootstrap.com/) for styling
- Hosted on [GitHub Pages](https://pages.github.com/)

## Local development

```bash
dotnet run --project src/SwaggerToJavaScriptObject/SwaggerToJavaScriptObject.csproj
```

Then open [http://localhost:5000](http://localhost:5000).

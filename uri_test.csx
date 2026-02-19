
using System;

var baseUri = new Uri("http://localhost:4566/_aws/execute-api/material-api/prod/");
var requestUri = new Uri("/api/v1/materials/components", UriKind.Relative);
var combined = new Uri(baseUri, requestUri);

Console.WriteLine($"Base: {baseUri}");
Console.WriteLine($"Request: {requestUri}");
Console.WriteLine($"Combined: {combined}");

var baseUriWithSlash = new Uri("http://localhost:4566/_aws/execute-api/material-api/prod/");
var requestUriNoSlash = new Uri("api/v1/materials/components", UriKind.Relative);
var combined2 = new Uri(baseUriWithSlash, requestUriNoSlash);

Console.WriteLine($"Base (/): {baseUriWithSlash}");
Console.WriteLine($"Request (no /): {requestUriNoSlash}");
Console.WriteLine($"Combined 2: {combined2}");

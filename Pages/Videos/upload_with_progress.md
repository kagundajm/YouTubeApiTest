
In a [previous post](#), we looked at ...

- change at page model class or action level `[RequestSizeLimit(262_144_000)] // 250MB`
- change at for all requests 
-https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1#kestrel-maximum-request-body-size-1
```c#
.ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.ConfigureKestrel((context, options) =>
            {
                // Handle requests up to 250 MB
                options.Limits.MaxRequestBodySize = 262_144_000;
            })
            .UseStartup<Startup>();
        });
```

Not yet out of the bushes. you will get a **Multipart body length limit exceeded exception** error. the actual exception will be **Multipart body length limit 134217728 exceeded**
    -[Multipart body length limit](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1#multipart-body-length-limit-1)
>> MultipartBodyLengthLimit sets the limit for the length of each multipart body. Form sections that exceed this limit throw an InvalidDataException when parsed. The default is 134,217,728 (128 MB).

To customize for all requests, update the limit in `Startup.ConfigureServices`
```c#
public void ConfigureServices(IServiceCollection services)
{
    services.Configure<FormOptions>(options =>
    {
        // Set the limit to 256 MB
        options.MultipartBodyLengthLimit = 268_435_456;
    });
}
```

Another option is to modify the limit on the page model.

```c#
// Set the limit to 256 MB
[RequestFormLimits(MultipartBodyLengthLimit = 268_435_456)]
public class BufferedSingleFileUploadPhysicalModel : PageModel
{
    ...
}
```


Fetch() API vs XMLHttpRequest
 - Fetch lacks has download progress but no upload progress

1. Add an id to submit button

<progress></progress>

- [XMLHttpRequest](https://javascript.info/xmlhttprequest)
- [XMLHttpRequest](https://xhr.spec.whatwg.org/#states)
- []()
- []()
- [Upload Large Files In ASP.NET Core](http://www.binaryintellect.net/articles/612cf2d1-5b3d-40eb-a5ff-924005955a62.aspx)
- []()
- [Upload files in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1)



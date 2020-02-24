Do you need a way for users to upload files to your .NET Core 3 app, and to track what they've uploaded in the past? This is a Razor Class Library that provides this capability using [DropzoneJS](https://www.dropzonejs.com/) and Azure blob storage integration in a single UI. The Nuget package is **AO.AzureUploadPortal.RCL**.

The benefit of the Razor Class Library approach is that the integration with your app is seamless. There's no additional authentication piece to implement, for example, nothing special to deploy, and no external dependencies apart from your Azure storage account. The abstract class approach this library uses has the benefit of requiring minimal application code, while affording a lot of flexibility in your implementation.

The limitation you have with a Razor Class Library is that you can't very easily override the provided UI. There are CSS classes you can implement, but you can't really affect the markup itself. I can tell you that as of this writing, the page layout is fairly primitive, and will be evolving. Please raise [issues](https://github.com/adamosoftware/AzureUploadPortal.RCL/issues) here if you have ideas or requests. The main page markup is [here](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/AzureUploader.RCL/Areas/UploadPortal/Pages/Index.cshtml).

![img](https://adamosoftware.blob.core.windows.net/images/azure-upload-portal-smaller.gif)

This repo contains a [SampleApp](https://github.com/adamosoftware/AzureUploadPortal.RCL/tree/master/SampleApp) showing a minimal implementation. Here's a walktrhough of the key points:

1. Install Nuget package AO.AzureUploadPortal.RCL in your .NET Core 3 app.

2. Create a class in your app based on [BlobManager](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/AzureUploader.RCL/Areas/UploadPortal/Services/BlobManager.cs). This is an abstract class, so there's some functionality you have to implement. See the sample implementation [MyBlobManager](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs). I'll talk in more detail about implementing `BlobManager`.

3. In your app startup, add your `BlobManager` implementation to your service collection. Example [here](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Startup.cs#L39).

4. Run your app and navigate to `/UploadPortal`. You should see a page like the one above that supports drag and drop uploading.

## About the repo
You can clone and run this repo yourself, but you'll need to provide Azure storage credentials in a json file called **/SampleApp/Config/storage.json**. It should be setup like this:

```json
{
  "StorageAccount": {
    "Name": "your account",
    "Key": "your key"
  }
}
```

## Implementing BlobManager
I'll go through the example [MyBlobManager](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs) piece by piece. The point of this class to fill in some details on how the upload portal page interacts with your blob storage account and how it logs history.

- In the [constructor](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L22), we pass the main `IConfiguration` object from which we get the Azure storage credentials as well as the database connection string. The connection string is needed because of I'm writing activity to a SQL Server table. It will be up to you exactly how you want to log history, but a database table made sense in my case.

```csharp
public MyBlobManager(IConfiguration config) : base(new StorageCredentials(config["StorageAccount:Name"], config["StorageAccount:Key"]))
{
    _connectionString = config.GetConnectionString("DefaultConnection");
}
```

- When logging and querying upload history, we'll need a way to access our database connection privately, so there's a [small method](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L27) for this. This is why we captured the connection string in the constructor.

- When users upload files, we need to know which Azure blob container they'll go to. All uploads will go to the [UploadContainerName](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L29):

```csharp
protected override string UploadContainerName => "sample-uploads";
```

- When users upload files, that `UploadContainerName` is not the final destination. Those files will be moved somewhere in your application storage structure. That's why there's a "submit" step that handles that movement, and logs when it was done. Part of that means knowing what container to move blobs when "submitted." That's why we override [GetSubmittedContainerAsync](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L31). In the sample app, I'm moving everything to a single container called `"submitted"`. In your app, you may have different containers for different tenants. The idea here is that you should be able to get the container name when given a user name. That's why the user name is passed as an argument.

```csharp
protected override Task<CloudBlobContainer> GetSubmittedContainerAsync(string userName) => GetContainerInternalAsync("submitted");
```

Notice that my example uses [GetContainerInternalAsync](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/AzureUploader.RCL/Areas/UploadPortal/Services/BlobManager.cs#L48). This is because this method handles the low-level interaction with the storage account, and verifies that the container exists.

- Optional, override [GetBlobName](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/AzureUploader.RCL/Areas/UploadPortal/Services/BlobManager.cs#L42). I want uploaded files to be in a folder the same as the user name. The default blob name is simply whatever file name was uploaded.

- As part of the logging process, we have to implement [LogSubmitStartedAsync](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L51) to create a placeholder log entry that will be updated when the submit process succeeds or fails. The sample app uses [Dapper.CX](https://github.com/adamosoftware/Dapper.CX) to perform the CRUD insert.

```csharp
protected override async Task<int> LogSubmitStartedAsync(SubmittedBlob submittedBlob)
{
    using (var cn = GetConnection())
    {
        return await cn.SaveAsync(submittedBlob);
    }
}
```

- Again, as part of the logging process, we have to implement [LogSubmitDoneAsync](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L39). This records either a successful submission or an error message on a given log Id. The sample app uses [Dapper.CX](https://github.com/adamosoftware/Dapper.CX) to perform the CRUD update.

```csharp
protected override async Task LogSubmitDoneAsync(int id, bool successful, string message = null)
{
    using (var cn = GetConnection())
    {
        var log = await cn.GetAsync<SubmittedBlob>(id);
        var ct = new ChangeTracker<SubmittedBlob>(log);
        log.IsSuccessful = successful;
        log.ErrorMessage = message;
        await cn.SaveAsync(log, ct);
    }
}
```

- The final piece of the logging functionality is being able to query upload history by user, returning paginated results. This is done with [QuerySubmittedBlobsAsync](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L59). The sample app uses [Dapper.QX](https://github.com/adamosoftware/Dapper.QX). The query class example is [here](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Queries/MySubmittedBlobs.cs). Note that my example doesn't actually use the `pageSize` argument. There is a hard-coded page size of 30 rows in the query class itself. That's kind of a bug; I might remove the `pageSize` argument from the [abstract method](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/AzureUploader.RCL/Areas/UploadPortal/Services/BlobManager.cs#L40).

```csharp
protected override async Task<IEnumerable<SubmittedBlob>> QuerySubmittedBlobsAsync(string userName, int pageSize = 30, int page = 0)
{
    using (var cn = GetConnection())
    {
        return await new MySubmittedBlobs() { UserName = userName, Page = page }.ExecuteAsync(cn);
    }
}
```

- Lastly, I left a comment in the source containing the [SQL CREATE TABLE](https://github.com/adamosoftware/AzureUploadPortal.RCL/blob/master/SampleApp/Services/MyBlobManager.cs#L68) statement I used to create my log table.

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class LoadingWorkTests
{
    [TestMethod]
    public void SceneUploadContinuationsResumeOnThePumpingThreadAndYieldBetweenFrames()
    {
        var thread = System.Environment.CurrentManagedThreadId;
        var steps = 0;
        var task = UploadAsync();
        Assert.AreEqual(0, steps);

        LoadingWork.Update();
        Assert.AreEqual(0, steps);
        for (var attempt = 0; attempt < 16 && steps == 0; attempt++) LoadingWork.Update();
        Assert.AreEqual(1, steps);
        Assert.IsFalse(task.IsCompleted);
        for (var attempt = 0; attempt < 16 && !task.IsCompleted; attempt++) LoadingWork.Update();
        Assert.IsTrue(task.IsCompleted);
        task.GetAwaiter().GetResult();
        Assert.AreEqual(2, steps);

        async Task UploadAsync()
        {
            await LoadingWork.NextFrame;
            Assert.AreEqual(thread, System.Environment.CurrentManagedThreadId);
            steps++;
            await LoadingWork.NextFrame;
            Assert.AreEqual(thread, System.Environment.CurrentManagedThreadId);
            steps++;
        }
    }

    [TestMethod]
    public void LoadingIndicatorRemainsUntilEveryOperationFinishes()
    {
        using var scene = LoadingWork.Begin("Preparing scene...");
        using var music = LoadingWork.Begin("Loading music...");
        LoadingWork.Update();
        StringAssert.Contains(LoadingWork.Status.Value, "Preparing scene...");
        StringAssert.Contains(LoadingWork.Status.Value, "Loading music...");

        scene.Dispose();
        LoadingWork.Update();
        Assert.AreEqual("Loading music...", LoadingWork.Status.Value);

        music.Dispose();
        LoadingWork.Update();
        Assert.AreEqual("", LoadingWork.Status.Value);
    }
}

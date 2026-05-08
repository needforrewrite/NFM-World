using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class ClientCarCollection(GraphicsDevice graphicsDevice, IReadOnlyCollection<IInGameCar> backendCars) : GameObject
{
    private Dictionary<IInGameCar, ClientCar> _clientCars = backendCars.ToDictionary(car => car, car => new ClientCar(graphicsDevice, car));
    
    private void RemoveExcessCars()
    {
        var toRemove = _clientCars.Keys.Except(backendCars).ToArray();
        foreach (var car in toRemove)
        {
            _clientCars.Remove(car);
        }
    }
    
    public override IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        foreach (var renderData in base.GetRenderData(lighting))
        {
            yield return renderData;
        }
        
        foreach (var car in backendCars)
        {
            if (!_clientCars.TryGetValue(car, out var clientCar))
            {
                clientCar = _clientCars[car] = new ClientCar(graphicsDevice, car);
            }

            foreach (var renderData in clientCar.GetRenderData(lighting))
            {
                yield return renderData;
            }
        }
    }

    public override void Render(Camera.Camera camera, Lighting? lighting)
    {
        base.Render(camera, lighting);
        
        foreach (var car in backendCars)
        {
            if (!_clientCars.TryGetValue(car, out var clientCar))
            {
                clientCar = _clientCars[car] = new ClientCar(graphicsDevice, car);
            }

            clientCar.Render(camera, lighting);
        }
    }

    public override void OnBeforeRender()
    {
        base.OnBeforeRender();
        
        foreach (var car in backendCars)
        {
            if (!_clientCars.TryGetValue(car, out var clientCar))
            {
                clientCar = _clientCars[car] = new ClientCar(graphicsDevice, car);
            }

            clientCar.OnBeforeRender();
        }
    }

    public override void GameTick(IStage? stage = null)
    {
        base.GameTick(stage);
        
        foreach (var car in backendCars)
        {
            if (!_clientCars.TryGetValue(car, out var clientCar))
            {
                clientCar = _clientCars[car] = new ClientCar(graphicsDevice, car);
            }

            clientCar.GameTick(stage);
            
            clientCar.Position = car.Position;
            clientCar.Rotation = car.Rotation;
        }

        RemoveExcessCars();
    }

    public ClientCar GetCar(IInGameCar car)
    {
        if (!_clientCars.TryGetValue(car, out var clientCar))
        {
            clientCar = _clientCars[car] = new ClientCar(graphicsDevice, car);
        }

        return clientCar;
    }
}
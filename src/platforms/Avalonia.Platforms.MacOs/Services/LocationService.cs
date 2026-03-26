using Avalonia.Platforms.Abstraction.Models;
using Avalonia.Platforms.Abstraction.Services;
using CoreLocation;

namespace Avalonia.Platforms.MacOs.Services;

public class LocationService : ILocationService
{
    public LocationService()
    {
        using var manager = new CLLocationManager();
        manager.RequestWhenInUseAuthorization();
    }

    public async Task<LocationCoordinate> GetLocationAsync()
    {
        using var manager = new CLLocationManager();
        var location = manager.Location ?? new CLLocation();
        manager.StopUpdatingLocation();
        return new LocationCoordinate()
        {
            Longitude = location.Coordinate.Longitude,
            Latitude = location.Coordinate.Latitude
        };
    }
}
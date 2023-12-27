Примеры использования API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.

**Пример создаёт настоящие QR-коды НСПК доступные к оплате.**

При желании можно создать свой [Личный Кабинет](sbp.online) и принимать оплаты на свой реальный счёт.

---

Доступно [Swagger UI](https://46.28.89.35:9904/index.html) с описанием API.

Клиент API написан 100% на [C#](https://ru.wikipedia.org/wiki/C_Sharp) под [.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Пакеты **nuget** клиента начинаются с префикса [ShtrihM.SbpPoint.Gateway.Api.Clients](https://www.nuget.org/packages?q=ShtrihM.SbpPoint.Gateway.Api.Clients)

Примеры в файле [Examples.cs](/Gateway/Examples.cs).

Все примеры оформлены как [NUnit](https://nunit.org/)-тесты для запуска в ОС Windows из-под [Visual Studio 2022](https://visualstudio.microsoft.com/ru/vs/) (проверено на версии 17.8.1).

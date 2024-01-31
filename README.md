Примеры использования API шлюза сервера обеспечения взаимодействия с [системой быстрых платежей](https://sbp.nspk.ru/).

**Пример создаёт настоящие QR-коды [НСПК](https://www.nspk.ru/) доступные к оплате.**

При желании можно создать свой [Личный Кабинет](https://sbp.online) и создавать свои QR-коды принимать оплаты на свой реальный счёт.

Пакеты nuget **.NET клиента** начинаются с префикса [ShtrihM.SbpPoint.Gateway](https://www.nuget.org/packages?q=ShtrihM.SbpPoint.Gateway)

---

Примеры в файле [Examples.cs](/Gateway/Examples.cs).

Примеры демонструют :
- [Создание](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/f09e5a64ee6b2a85b1fb5199de704f8085c1a7b3/Gateway/Examples.cs#L315) QR-Кода.
- [Запрос статуса оплаты](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/f09e5a64ee6b2a85b1fb5199de704f8085c1a7b3/Gateway/Examples.cs#L332) QR-Кода.
- [Возврат](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/f09e5a64ee6b2a85b1fb5199de704f8085c1a7b3/Gateway/Examples.cs#L355) денег оплаченного QR-Кода.
- [Частичный возврат](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/f09e5a64ee6b2a85b1fb5199de704f8085c1a7b3/Gateway/Examples.cs#L424) денег оплаченного QR-Кода.
- [Запрос статуса возврата](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/f09e5a64ee6b2a85b1fb5199de704f8085c1a7b3/Gateway/Examples.cs#L370) денег оплаченного QR-Кода.
- Ручная [отмена](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/a3d37499dc4e127fe747996c4ab504517d72ec05/Gateway/Examples.cs#L210) QR-Кода - **расширение функционала QR-кода и отсутствует у НСПК.**
- Автоматическая [отмена](https://github.com/KlestovAlexej/SbpPoint.Examples/blob/a3d37499dc4e127fe747996c4ab504517d72ec05/Gateway/Examples.cs#L252) (по TTL) QR-Кода - **расширение функционала QR-кода и не полностью присутствует у НСПК.**

---

В файле [ExamplesGatewayAdapter.cs](/Gateway/ExamplesGatewayAdapter.cs).

Показаны примеры создания QR-кодов в терминах ПНКО «ЭЛПЛАТ».

---

Доступен [Swagger UI](https://46.28.89.35:9904/index.html) с описанием API.

Клиент API написан 100% на [C#](https://ru.wikipedia.org/wiki/C_Sharp) под [.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

Пакеты **nuget** клиента начинаются с префикса [ShtrihM.SbpPoint.Gateway.Api.Clients](https://www.nuget.org/packages?q=ShtrihM.SbpPoint.Gateway.Api.Clients)

Все примеры оформлены как [NUnit](https://nunit.org/)-тесты (проверено на [Visual Studio 2022](https://visualstudio.microsoft.com/ru/vs/) версии 17.8.1).

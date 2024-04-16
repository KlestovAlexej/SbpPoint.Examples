using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NUnit.Framework;
using RestSharp;
using ShtrihM.SbpPoint.Gateway.Adapter.Api.Clients;
using ShtrihM.Wattle3.Testing;
using ShtrihM.SbpPoint.Gateway.Adapter.Api.Common.Dtos.CommandReturns.QrDynamic;
using ShtrihM.SbpPoint.Gateway.Adapter.Api.Common.Dtos.Commands.QrDynamic;
using ShtrihM.Wattle3.Json.Extensions;
using ShtrihM.Wattle3.Utils;

namespace ShtrihM.SbpPoint.Examples.Gateway;

/// <summary>
/// Примеры использования API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
/// </summary>
[TestFixture]
public class ExamplesGatewayAdapter : BaseExamples
{
    /// <summary>
    /// Ключ API.
    /// </summary>
    private static readonly string ApiKey = "rScnOvVCybo0UYCsIxnQaRd8HXIAbQUH2EuGcthG7tcVhy4buPkmggcsD1l9VSR9";

    /// <summary>
    /// Настроенный клиент HTTPS.
    /// </summary>
    private readonly RestClient m_restClient;

    public ExamplesGatewayAdapter()
    {
        var rootServerCertificateHttpsBytes = File.ReadAllBytes("ca.shtrihm.sbp.gateway.adapter.servers.cer");
        var rootServerCertificateHttps = new X509Certificate2(rootServerCertificateHttpsBytes);

        m_restClient =
            new RestClient(
                useClientFactory: true,
                configureRestClient:
                options =>
                {
                    options.BaseUrl = new Uri("https://46.28.89.35:9961");
                    options.ConfigureMessageHandler =
                        _ => GatewayClient.NewHttpClientHandler(rootServerCertificateHttps, true);
                },
                configureSerialization:
                config => { GatewayClient.UpdateSerializerConfig(config); });
    }

    /// <summary>
    /// Получить описание сервера.
    /// Позволяет проверить сетевое соединение с сервером.
    /// </summary>
    [Test]
    public async Task GetDescriptionAsync()
    {
        using var client = new GatewayClient(m_restClient);

        var description = await client.GetDescriptionAsync();

        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Проверка связи.
    /// Позволяет проверить сетевое соединение с сервером и работоспособность ключа API.
    /// </summary>
    [Test]
    public async Task PingAsync()
    {
        using var client = new GatewayClient(m_restClient);

        var description = await client.PingAsync(ApiKey);

        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// QR-код в терминах ПНКО «ЭЛПЛАТ».
    /// Создание динамического QR-кода и ожидание его отмены по истечению TTL.
    /// </summary>
    [Test]
    public async Task Elplat_DynamicQr_Ttl_Expired()
    {
        using var client = new GatewayClient(m_restClient);

        var qrDynamicCreate = GetCommandForElplatQrDynamicCreate();

        var qrDynamicCreateResult = await QrDynamicCreateAsync(client, qrDynamicCreate);

        var qrDynamicStatusRead =
            new GwCommandQrDynamicStatusRead
            {
                Key = qrDynamicCreateResult.Key,
            };

        QrDynamicWaitForEnd(client, qrDynamicStatusRead);

        // Проверка отмены динамического QR-кода
        var qrDynamicStatus = await QrDynamicStatusReadAsync(client, qrDynamicStatusRead);
        Assert.AreEqual(GwQrDynamicPaymentStates.Rejected, qrDynamicStatus.PaymentState);
    }

    /// <summary>
    /// QR-код в терминах ПНКО «ЭЛПЛАТ».
    /// Создание динамического QR-кода и ожидание его оплаты.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Elplat_DynamicQr_Accepted()
    {
        using var client = new GatewayClient(m_restClient);

        var qrDynamicCreate = GetCommandForElplatQrDynamicCreate();

        var qrDynamicCreateResult = await QrDynamicCreateAsync(client, qrDynamicCreate);

        try
        {
            ShowQrImage(qrDynamicCreateResult.Data);

            var qrDynamicStatusRead =
                new GwCommandQrDynamicStatusRead
                {
                    Key = qrDynamicCreateResult.Key,
                };

            QrDynamicWaitForEnd(client, qrDynamicStatusRead);

            // Проверка успешной оплаты динамического QR-кода
            var qrDynamicStatus = await QrDynamicStatusReadAsync(client, qrDynamicStatusRead);
            Assert.AreEqual(GwQrDynamicPaymentStates.Accepted, qrDynamicStatus.PaymentState);
        }
        finally
        {
            DeleteQrImage();
        }
    }

    #region Helpers

    /// <summary>
    /// Стение статуса динамического QR-кода.
    /// </summary>
    private async Task<GwCommandReturnQrDynamicStatusRead> QrDynamicStatusReadAsync(
        IGatewayClient client,
        GwCommandQrDynamicStatusRead qrDynamicStatusRead)
    {
        var commandReturn =
            await client.CommandRunAsync(
                ApiKey,
                qrDynamicStatusRead);
        var commandReturnQrDynamicStatusRead = commandReturn as GwCommandReturnQrDynamicStatusRead;
        Assert.IsNotNull(commandReturnQrDynamicStatusRead);


        return commandReturnQrDynamicStatusRead;
    }

    /// <summary>
    /// ПНКО «ЭЛПЛАТ».
    /// Создать команду для создания динамического QR-кода.
    /// </summary>
    private GwCommandIdempotentQrDynamicCreate GetCommandForElplatQrDynamicCreate()
    {
        var qrDynamicCreate =
            new GwCommandIdempotentQrDynamicCreate
            {
                Key = Guid.NewGuid(),
                Amount = 100,
                Purpose = "Test",
                RedirectUrl = "https://www.shtrih-m.ru/",
                Ttl = 5,
                Domain =
                    // Параметры динамического QR-кода специфические для ПНКО «ЭЛПЛАТ»
                    new GwCommandIdempotentQrDynamicCreateDomainAsElplat
                    {
                        Email = "mail@mail.com",
                        Phone = "8800087654321",
                    },
            };

        return qrDynamicCreate;
    }

    /// <summary>
    /// Дождаться терминального статуса динамического QR-кода.
    /// </summary>
    private void QrDynamicWaitForEnd(
        IGatewayClient client,
        GwCommandQrDynamicStatusRead qrDynamicStatusRead)
    {
        // Дождаться терминального статуса динамического QR-кода
        WaitHelpers.TimeOut(
            () => ((GwCommandReturnQrDynamicStatusRead)client
                    .CommandRunAsync(
                        ApiKey,
                        qrDynamicStatusRead)
                    .SafeGetResult())
                .PaymentState != GwQrDynamicPaymentStates.InProcess,
            TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Создание динамического QR-кода.
    /// </summary>
    private async Task<GwCommandIdempotentReturnQrDynamicCreate> QrDynamicCreateAsync(
        IGatewayClient client,
        GwCommandIdempotentQrDynamicCreate qrDynamicCreate)
    {
        // Создание динамического QR-кода
        var commandIdempotentProcessingInfo =
            await client.CommandIdempotentRunAsync(
                ApiKey,
                qrDynamicCreate);

        if (false == commandIdempotentProcessingInfo.IsCompleted)
        {
            Console.WriteLine("Сервер перегружен. Ждём завершение команды создания динамического QR-кода...");

            // Дождаться завершения команды создания динамического QR-кода
            WaitHelpers.TimeOut(
                () => client.CommandIdempotentRunAsync(
                        ApiKey,
                        qrDynamicCreate)
                    .SafeGetResult()
                    .IsCompleted,
                TimeSpan.FromMinutes(qrDynamicCreate.Ttl));

            // Проверить завершение команды создания динамического QR-кода
            commandIdempotentProcessingInfo =
                await client.CommandIdempotentRunAsync(
                    ApiKey,
                    qrDynamicCreate);
            Assert.IsTrue(commandIdempotentProcessingInfo.IsCompleted);
        }

        // Проверить что динамический QR-код создан успешно
        var returnQrDynamicCreate = commandIdempotentProcessingInfo.Return as GwCommandIdempotentReturnQrDynamicCreate;
        Assert.IsNotNull(returnQrDynamicCreate);
        Assert.IsTrue(returnQrDynamicCreate.IsSuccess);

        return returnQrDynamicCreate;
    }

    #endregion
}
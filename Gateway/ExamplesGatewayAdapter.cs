using System;
using System.Diagnostics.CodeAnalysis;
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
using QRCoder;
using System.Diagnostics;

namespace ShtrihM.SbpPoint.Examples.Gateway;

/// <summary>
/// Примеры использования API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
/// </summary>
[TestFixture]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
public class ExamplesGatewayAdapter
{
    /// <summary>
    /// Базовый URL API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
    /// </summary>
    private static readonly string BaseAddress = "https://46.28.89.35:9961";

    /// <summary>
    /// Ключ API.
    /// </summary>
    private static readonly string ApiKey = "MxPnOqO92bqvtvz+h+Jq/qRAtPAKZ2c2y4hUca37gBZ4OwObpUlK0xFKFhlwz2BJ";

    /// <summary>
    /// Публичный корневой сертификат сервера для HTTPS.
    /// </summary>
    private readonly X509Certificate2 m_rootServerCertificateHttps;

    /// <summary>
    /// Настроенный клиент HTTPS.
    /// </summary>
    private readonly RestClient m_restClient;

    private readonly string m_tempPath;

    public ExamplesGatewayAdapter()
    {
        m_tempPath = Path.GetTempPath();

        var rootServerCertificateHttpsBytes = File.ReadAllBytes("ca.shtrihm.sbp.gateway.adapter.servers.cer");
        m_rootServerCertificateHttps = new X509Certificate2(rootServerCertificateHttpsBytes);

        var handler = GatewayClient.NewHttpClientHandler(m_rootServerCertificateHttps, true);
        m_restClient =
            new RestClient(
                useClientFactory: true,
                configureRestClient:
                options =>
                {
                    options.BaseUrl = new Uri(BaseAddress);
                    options.ConfigureMessageHandler = _ => handler;
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
        var commandReturn =
            await client.CommandRunAsync(
                    ApiKey,
                    qrDynamicStatusRead);
        var commandReturnQrDynamicStatusRead = commandReturn as GwCommandReturnQrDynamicStatusRead;
        Assert.IsNotNull(commandReturnQrDynamicStatusRead);
        Assert.AreEqual(GwQrDynamicPaymentStates.Rejected, commandReturnQrDynamicStatusRead.PaymentState);
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

        ShowQrImage(qrDynamicCreateResult.Data);

        try
        {
            var qrDynamicStatusRead =
                new GwCommandQrDynamicStatusRead
                {
                    Key = qrDynamicCreateResult.Key,
                };

            QrDynamicWaitForEnd(client, qrDynamicStatusRead);

            // Проверка оплаты динамического QR-кода
            var commandReturn =
                await client.CommandRunAsync(
                    ApiKey,
                    qrDynamicStatusRead);
            var commandReturnQrDynamicStatusRead = commandReturn as GwCommandReturnQrDynamicStatusRead;
            Assert.IsNotNull(commandReturnQrDynamicStatusRead);
            Assert.AreEqual(GwQrDynamicPaymentStates.Accepted, commandReturnQrDynamicStatusRead.PaymentState);
        }
        finally
        {
            DeleteQrImage();
        }
    }

    #region Helpers

    private void DeleteQrImage()
    {
        var fileNmae = Path.Combine(m_tempPath, "QR.png");
        if (File.Exists(fileNmae))
        {
            File.Delete(fileNmae);
        }
    }

    private void ShowQrImage(string data)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeAsPngByteArr = qrCode.GetGraphic(20);
        var fileNmae = Path.Combine(m_tempPath, "QR.png");
        if (File.Exists(fileNmae))
        {
            File.Delete(fileNmae);
        }
        File.WriteAllBytes(fileNmae, qrCodeAsPngByteArr);
        Console.WriteLine(fileNmae);
        Process.Start("explorer.exe", fileNmae);
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
            // Дождаться завершения команды динамического QR-кода
            WaitHelpers.TimeOut(
                () => client.CommandIdempotentRunAsync(
                        ApiKey,
                        qrDynamicCreate)
                    .SafeGetResult()
                    .IsCompleted,
                TimeSpan.FromMinutes(qrDynamicCreate.Ttl));

            // Проверить завершение команды динамического QR-кода
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

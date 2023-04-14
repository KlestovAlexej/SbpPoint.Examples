using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NUnit.Framework;
using ShtrihM.SbpPoint.Gateway.Api.Clients;
using ShtrihM.Wattle3.Json.Extensions;
using RestSharp;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments.AutomationDynamicQrs;
using ShtrihM.Wattle3.Testing;
using ShtrihM.Wattle3.Utils;
using System.Diagnostics;
using QRCoder;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments.Refund;

namespace ShtrihM.SbpPoint.Examples.Gateway;

/// <summary>
/// Примеры использования API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
/// </summary>
[TestFixture]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
public class Examples
{
    /// <summary>
    /// Базовый URL API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
    /// </summary>
    private static readonly string BaseAddress = "https://46.28.89.35:9904";

    /// <summary>
    /// Ключ API.
    /// </summary>
    private static readonly string ApiKey = @"EN1-1:CAe3ey2KdneylslaZHKD7k2vDV4mGxEcBUBnyfG77RfEl+lRiYjTNlL7J+Y+Sg7B";

    /// <summary>
    /// Публичный корневой сертификат сервера для HTTPS.
    /// </summary>
    private readonly X509Certificate2 m_rootServerCertificateHttps;

    /// <summary>
    /// Настроенный клиент HTTPS.
    /// </summary>
    private readonly RestClient m_restClient;

    private readonly string m_tempPath;

    public Examples()
    {
        m_tempPath = Path.GetTempPath();

        var rootServerCertificateHttpsBytes = File.ReadAllBytes(@"root.sbppoint.gateway.server.cer");
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
    /// Создание <see cref="RestClient"/> руками.
    /// </summary>
    [Test]
    public async Task Example_Manual_RestClient()
    {
        var customTrustStore =
            new X509Certificate2Collection
            {
                m_rootServerCertificateHttps
            };
        var customChainPolicy =
            new X509ChainPolicy
            {

                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.IgnoreWrongUsage,
                TrustMode = X509ChainTrustMode.CustomRootTrust,
            };
        customChainPolicy.CustomTrustStore.AddRange(customTrustStore);

        var handler =
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (_, certificate, chain, _) =>
                    {
                        var customChain =
                            new X509Chain
                            {
                                ChainPolicy = customChainPolicy
                            };
                        customChain.Build(certificate);

                        foreach (var element in customChain.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == m_rootServerCertificateHttps!.Thumbprint)
                            {
                                return true;
                            }
                        }

                        foreach (var element in chain.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == m_rootServerCertificateHttps.Thumbprint)
                            {
                                return true;
                            }
                        }

                        return false;
                    },
            };

        var restClient =
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

        using var client = new GatewayClient(restClient, true);
        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Получить описание сервера.
    /// </summary>
    [Test]
    public async Task Example_GetDescriptionAsync()
    {
        using var client = new GatewayClient(m_restClient);

        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Чтение публичной информации ключа API.
    /// </summary>
    [Test]
    public async Task Example_SupportApiKeysReadAsync_Public()
    {
        using var client = new GatewayClient(m_restClient);

        var info = await client.SupportApiKeysReadAsync(ApiKey);
        Assert.IsNotNull(info);
        Console.WriteLine(info.ToJsonText(true));
    }

    /// <summary>
    /// Чтение приватной информации ключа API.
    /// </summary>
    [Test]
    public async Task Example_SupportApiKeysReadAsync_Private()
    {
        using var client = new GatewayClient(m_restClient);

        var info = await client.SupportApiKeysReadAsync(ApiKey, ApiKey);
        Assert.IsNotNull(info);
        Console.WriteLine(info.ToJsonText(true));
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Ручная отмена платежа.
    /// </summary>
    [Test]
    public async Task Example_Payment_AutomationDynamicQr_Cancel_Manual()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        // Отмена платежа.
        await client.PaymentsAutomationDynamicQrsCancelAsync(
            ApiKey,
            new AutomationDynamicQrCancel
            {
                Id = payment.Id,
            });

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);
        Assert.IsFalse(payment.Status.IsSuccess);
        Assert.AreEqual(PaymentStatus.Canceled, payment.Status.Status);
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Автоматическая отмена платежа.
    /// </summary>
    [Test]
    public async Task Example_Payment_AutomationDynamicQr_Cancel_Auto()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 1,
                Purpose = "Тест (10 рублей)"
            });

        // Ждём завершения платежа - примерно 1 минута.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(5));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);
        Assert.IsFalse(payment.Status.IsSuccess);
        Assert.AreEqual(PaymentStatus.Rejected, payment.Status.Status);
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Оплата платежа.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Example_Payment_AutomationDynamicQr_Image()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        ShowQrImage(payment.Link);

        // Ждём завершения платежа - примерно 5 минут.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);

        DeleteQrImage();
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Оплата платежа.
    /// Возврат платежа.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Example_Refund()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        ShowQrImage(payment.Link);

        // Ждём завершения платежа - примерно 5 минут.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);

        DeleteQrImage();

        // Возврат платежа.
        var refund = await client.PaymentsRefundsRefundAsync(
            ApiKey,
            new RefundCreate
            {
                Amount = 1000,
                Id = payment.Id,
                Purpose = "Тест",
            });

        // Ждём завершения возврата.
        WaitHelpers.TimeOut(
            () => client.PaymentsRefundsReadAsync(ApiKey, refund.Id).SafeGetResult().IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса возврата.
        refund = await client.PaymentsRefundsReadAsync(ApiKey, refund.Id);
        Assert.IsNotNull(refund);
        Console.WriteLine(refund.ToJsonText(true));
    }

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
}

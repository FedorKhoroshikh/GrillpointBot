namespace GrillpointBot.Core.Models;

public enum FlowState
{
    Idle,               // пользователь не в процессе
    Browsing,           // выбирает меню
    ViewingItems,       // смотрит товары категории
    InCart,             // редактирует корзину
    CheckoutMethod,     // выбор доставки/самовывоза
    CheckoutAddress,    // ввод адреса
    CheckoutTime,       // ввод времени
    CheckoutPhone,      // ввод телефона
    Confirm             // подтверждение заказа
}

public sealed class Session
{
    public long UserId { get; set; }
    public FlowState State { get; set; } = FlowState.Idle;
    public List<OrderLine> Cart { get; set; } = []; // Корзина
    public DeliveryInfo DraftDelivery { get; set; } = new(); // черновик адреса/контактов

    public string? Comment { get; set; }                  // пожелания

    public int? LastMessageId { get; set; }               // ID текущего поста в Telegram
    public DateTime LastUpdatedUtc { get; set; }
}
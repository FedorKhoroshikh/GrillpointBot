using GrillpointBot.Core.Interfaces;

namespace GrillpointBot.Core.Models;

public enum FlowState
{
    Idle,               // пользователь не в процессе
    Browsing,           // выбирает меню
    ViewingItems,       // смотрит товары категории
    InCart,             // редактирует корзину
    CommentPending,     // комментарий к заказу
    CheckoutMethod,     // выбор доставки/самовывоза
    
    CheckoutAddress,    // ввод адреса
    
    CheckoutAddressChoice,   // выбор способа: вручную или по гео
    CheckoutAddressManual,   // ввод адреса текстом
    CheckoutAddressGeo,      // ожидание геолокации от Telegram
    CheckoutAddressConfirm,  // подтверждение адреса (точка/текст)
    PickupPreview,           // показ точки самовывоза
    PickupConfirm,           // подтверждение самовывоза
    
    CheckoutTime,       // ввод времени
    CheckoutPhone,      // ввод телефона
    Confirm             // подтверждение заказа
    
}

public sealed class Session
{
    public long UserId { get; set; }                                 // ID пользователя
    public string? UserNick { get; set; } = "";                       // Никнейм пользователя
    public FlowState State { get; set; } = FlowState.Idle;           // текущий статус в workflow заказа
    
    public List<OrderLine> Cart { get; set; } = [];                  // итоговая корзина
    public Dictionary<string, int> DraftQty { get; } = new();        // qty по карточкам (id -> qty)
    public DeliveryInfo DraftDelivery { get; set; } = new();         // черновик данных для доставки

    // карточки/категории/корзина
    public List<int> ItemMessageIds { get; } = [];                  // сообщения с карточками
    public int? CategoriesMessageId { get; set; }                   // сообщение с «Выберите категорию»
    public int? CartMessageId { get; set; }                         // сообщение корзины
    
    // комментарий
    public string? Comment { get; set; }                             // комментарий к заказу
    public string? DraftComment { get; set; }                        // черновик комментария к заказу
    public List<int> CommentMessageIds { get; set; } = [];           // ID сообщений истории изменения комментария
    public List<int> CheckoutMessageIds { get; set; } = [];          // ID сообщений выбора получения заказа и заполнения контактных данных
    
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;  // таймкод последнего действия в сессии
    
    public async Task ClearAsync()
    {
        Cart.Clear();
        DraftQty.Clear();
        Comment = DraftComment = null;
        CartMessageId = null;
        CommentMessageIds.Clear();
        State = FlowState.Idle;
    }
}
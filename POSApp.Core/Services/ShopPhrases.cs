namespace POSApp.Core.Services
{
    /// <summary>One cashier-facing phrase, in English and Spanish.</summary>
    public sealed record ShopPhrase(string Key, string Group, string English, string Spanish);

    /// <summary>
    /// The words the shop shows on the register. English and Spanish are built in. A shop can
    /// replace any line from first-run setup or Admin → Business Settings → Wording.
    /// </summary>
    public static class ShopPhrases
    {
        public const string English = "en";
        public const string Spanish = "es";

        public static IReadOnlyList<ShopPhrase> Catalog { get; } = new ShopPhrase[]
        {
            new("login.subtitle", "Login", "Sign in to continue", "Inicie sesión para continuar"),
            new("login.username", "Login", "Username", "Usuario"),
            new("login.password", "Login", "Password", "Contraseña"),
            new("login.signIn", "Login", "Sign in", "Entrar"),
            new("login.exit", "Login", "Exit", "Salir"),

            new("setup.welcome", "Setup", "Welcome — let's set up your shop", "Bienvenido — configuremos su tienda"),
            new("setup.hint", "Setup", "This takes a minute. You can change everything later in Admin → Business Settings.", "Toma un minuto. Puede cambiar todo después en Admin → Configuración del negocio."),
            new("setup.shop", "Setup", "1. Your shop", "1. Su tienda"),
            new("setup.shopName", "Setup", "Shop name *", "Nombre de la tienda *"),
            new("setup.phone", "Setup", "Phone (optional)", "Teléfono (opcional)"),
            new("setup.currency", "Setup", "Currency", "Moneda"),
            new("setup.address", "Setup", "Address (optional)", "Dirección (opcional)"),
            new("setup.tax", "Setup", "Sales tax rate %", "Tasa de impuesto %"),
            new("setup.owner", "Setup", "2. Owner account", "2. Cuenta del dueño"),
            new("setup.ownerHint", "Setup", "You'll use this to sign in. Add cashiers later from Admin → Users.", "Con esto inicia sesión. Agregue cajeros después en Admin → Usuarios."),
            new("setup.username", "Setup", "Username *", "Usuario *"),
            new("setup.password", "Setup", "Password *", "Contraseña *"),
            new("setup.confirm", "Setup", "Confirm password *", "Confirme la contraseña *"),
            new("setup.samples", "Setup", "3. Sample data (optional)", "3. Datos de ejemplo (opcional)"),
            new("setup.samplesCheck", "Setup", "Load the sample US pharmacy catalog so I can try sales right away", "Cargar el catálogo de ejemplo de farmacia para probar ventas ahora"),
            new("setup.language", "Setup", "4. Language and wording", "4. Idioma y textos"),
            new("setup.languageHint", "Setup", "Pick the language cashiers see. You can edit any line, and change it later in Business Settings → Wording.", "Elija el idioma de los cajeros. Puede editar cualquier frase y cambiarla después en Configuración → Textos."),
            new("setup.languageLabel", "Setup", "Language", "Idioma"),
            new("setup.finish", "Setup", "Save and continue", "Guardar y continuar"),

            new("sale.save", "Sale", "Save Sale", "Guardar venta"),
            new("sale.print", "Sale", "Print", "Imprimir"),
            new("sale.split", "Sale", "Split…", "Dividir…"),
            new("sale.new", "Sale", "New Sale", "Nueva venta"),
            new("sale.close", "Sale", "Close", "Cerrar"),
            new("sale.gift", "Sale", "Gift card", "Tarjeta de regalo"),
            new("sale.quick", "Sale", "Quick Sale", "Venta rápida"),
            new("sale.empty", "Sale", "Scan a barcode or pick a product above to add items", "Escanee un código o elija un producto para agregar"),

            new("pay.title", "Payment", "Split payment", "Pago dividido"),
            new("pay.remaining", "Payment", "Remaining", "Restante"),
            new("pay.with", "Payment", "Pay with", "Pagar con"),
            new("pay.amount", "Payment", "Amount", "Importe"),
            new("pay.cash", "Payment", "Cash", "Efectivo"),
            new("pay.card", "Payment", "Card", "Tarjeta"),
            new("pay.check", "Payment", "Check", "Cheque"),
            new("pay.account", "Payment", "Charge Account", "Cuenta de la casa"),
            new("pay.gift", "Payment", "Gift card", "Tarjeta de regalo"),
            new("pay.loyalty", "Payment", "Loyalty", "Puntos"),
            new("pay.add", "Payment", "Add", "Agregar"),
            new("pay.done", "Payment", "Done", "Listo"),
            new("pay.giftCode", "Payment", "Gift card code", "Código de la tarjeta"),
            new("pay.remove", "Payment", "Remove", "Quitar"),

            new("gift.title", "Gift card", "Sell a gift card", "Vender una tarjeta de regalo"),
            new("gift.amount", "Gift card", "Amount to put on the card", "Importe de la tarjeta"),
            new("gift.name", "Gift card", "Recipient name (optional)", "Nombre del destinatario (opcional)"),
            new("gift.add", "Gift card", "Add to this sale", "Agregar a esta venta"),
            new("gift.cancel", "Gift card", "Cancel", "Cancelar"),
            new("gift.line", "Gift card", "Gift card", "Tarjeta de regalo"),
            new("gift.hint", "Gift card", "The code is printed on the receipt. The customer pays for it like any other item. Gift cards are not taxed.", "El código se imprime en el recibo. El cliente lo paga como cualquier artículo. La tarjeta no lleva impuesto."),

            new("loyalty.enroll", "Loyalty", "Enrolled in loyalty", "Inscrito en puntos"),
            new("loyalty.points", "Loyalty", "Loyalty points", "Puntos de lealtad"),
            new("loyalty.perDollar", "Loyalty", "Points earned per $1 spent", "Puntos ganados por cada $1"),
            new("loyalty.perReward", "Loyalty", "Points needed for $1 off", "Puntos necesarios por $1 de descuento"),
            new("loyalty.hint", "Loyalty", "Points are earned on merchandise. Gift cards and the part paid with points do not earn points. 100 points = $1 off unless you change it here.", "Los puntos se ganan en mercancía. Las tarjetas de regalo y lo pagado con puntos no ganan puntos. 100 puntos = $1 de descuento, salvo que lo cambie aquí."),

            new("wording.title", "Wording", "Wording", "Textos"),
            new("wording.hint", "Wording", "These are the words on the login screen, the sale screen and payment. Edit a line to use your own wording. Empty lines go back to the language you picked.", "Estas son las frases del inicio de sesión, la venta y el pago. Edite una línea para usar su propio texto. Una línea vacía vuelve al idioma elegido."),
        };

        public static string Resolve(string? language, string key, IReadOnlyDictionary<string, string>? overrides)
        {
            if (overrides != null && overrides.TryGetValue(key, out var custom) && !string.IsNullOrWhiteSpace(custom))
                return custom.Trim();
            var phrase = Catalog.FirstOrDefault(p => p.Key == key);
            if (phrase == null) return key;
            return string.Equals(language, Spanish, StringComparison.OrdinalIgnoreCase) ? phrase.Spanish : phrase.English;
        }

        public static string BuiltIn(string? language, string key)
        {
            var phrase = Catalog.FirstOrDefault(p => p.Key == key);
            if (phrase == null) return key;
            return string.Equals(language, Spanish, StringComparison.OrdinalIgnoreCase) ? phrase.Spanish : phrase.English;
        }
    }

    /// <summary>The shop's language, edited phrases, and loyalty rates. Stored in the database.</summary>
    public sealed record ShopTextSettings(
        string Language,
        IReadOnlyDictionary<string, string> Overrides,
        decimal PointsPerDollar,
        int PointsPerRewardDollar)
    {
        public static ShopTextSettings Default { get; } = new(ShopPhrases.English, new Dictionary<string, string>(), 1m, 100);

        public bool IsSpanish => string.Equals(Language, ShopPhrases.Spanish, StringComparison.OrdinalIgnoreCase);
    }
}

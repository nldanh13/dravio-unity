namespace Dravio.UI
{
    /// <summary>
    /// Sprite roles of the VIS.2a theme. IDs are stable: AI3 sprite slots and ui-sprite-slots.json use the
    /// same names. Append new roles at the end; never renumber (values are serialized in theme assets).
    /// </summary>
    public enum DravioUiRole
    {
        PrimaryButton=0,
        SecondaryButton=1,
        RewardButton=2,
        DangerButton=3,
        CloseButton=4,
        BackButton=5,
        Tab=6,
        TabActive=7,
        TabIndicator=8,
        Panel=9,
        HeaderBar=10,
        PageBackground=11,
        Card=12,
        SlotCommon=13,
        SlotRare=14,
        SlotEpic=15,
        SlotLegendary=16,
        ProgressFrame=17,
        ProgressFill=18,
        ProgressHighlight=19,
        Divider=20,
        Portrait=21,
        Icon=22,
    }

    /// <summary>Text roles for colour only (no sprite). Text is never baked into art.</summary>
    public enum DravioTextRole
    {
        Title=0,
        Body=1,
        Muted=2,
        ButtonLabel=3,
        TabLabel=4,
        TabLabelActive=5,
    }

    public static class DravioUiRoles
    {
        /// <summary>Roles drawn as 9-slice (Image.Type.Sliced). Everything else is Simple.</summary>
        public static bool IsSliced(DravioUiRole r)=>r switch
        {
            DravioUiRole.TabIndicator or DravioUiRole.Portrait or DravioUiRole.Icon or
            DravioUiRole.CloseButton or DravioUiRole.BackButton or DravioUiRole.ProgressHighlight=>false,
            _=>true,
        };

        /// <summary>Roles that must keep the sprite's aspect ratio (icons, round buttons, portrait).</summary>
        public static bool PreserveAspect(DravioUiRole r)=>r is DravioUiRole.Icon or DravioUiRole.Portrait
            or DravioUiRole.CloseButton or DravioUiRole.BackButton or DravioUiRole.TabIndicator;

        public static bool IsButton(DravioUiRole r)=>r is DravioUiRole.PrimaryButton or DravioUiRole.SecondaryButton
            or DravioUiRole.RewardButton or DravioUiRole.DangerButton or DravioUiRole.CloseButton or DravioUiRole.BackButton
            or DravioUiRole.Tab or DravioUiRole.TabActive;
    }
}

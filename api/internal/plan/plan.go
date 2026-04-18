package plan

type Tier string

const (
	TierFree    Tier = "free"
	TierPremium Tier = "premium"
)

type Capabilities struct {
	AvatarSlots      int  `json:"avatarSlots"`
	AccessorySlots   int  `json:"accessorySlots"`
	WorldSlots       int  `json:"worldSlots"`
	VariantSlots     int  `json:"variantSlots"`
	MyObjectSlots    int  `json:"myObjectSlots"`
	FriendLimit      int  `json:"friendLimit"`
	MaxPlayersLimit  int  `json:"maxPlayersLimit"`
	SessionMinutes   int  `json:"sessionMinutes"`
	AfkEnabled       bool `json:"afkEnabled"`
	BackgroundCall   bool `json:"backgroundCall"`
	InviteRoomCreate bool `json:"inviteRoomCreate"`
	NameChange       bool `json:"nameChange"`
}

var planConfig = map[Tier]Capabilities{
	TierFree: {
		AvatarSlots:      10,
		AccessorySlots:   10,
		WorldSlots:       5,
		VariantSlots:     10,
		MyObjectSlots:    10,
		FriendLimit:      100,
		MaxPlayersLimit:  6,
		SessionMinutes:   90,
		AfkEnabled:       true,
		BackgroundCall:   false,
		InviteRoomCreate: false,
		NameChange:       false,
	},
	TierPremium: {
		AvatarSlots:      100,
		AccessorySlots:   100,
		WorldSlots:       50,
		VariantSlots:     100,
		MyObjectSlots:    100,
		FriendLimit:      1000,
		MaxPlayersLimit:  24,
		SessionMinutes:   720,
		AfkEnabled:       false,
		BackgroundCall:   true,
		InviteRoomCreate: true,
		NameChange:       true,
	},
}

var planTierOrder = map[Tier]int{
	TierFree:    0,
	TierPremium: 1,
}

func GetCapabilities(tier Tier) Capabilities {
	if caps, ok := planConfig[tier]; ok {
		return caps
	}
	return planConfig[TierFree]
}

func TierAtLeast(userTier, required Tier) bool {
	return planTierOrder[userTier] >= planTierOrder[required]
}

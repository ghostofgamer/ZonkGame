# -*- coding: utf-8 -*-
"""
Подключение новых аватарок и рамок (06.11.2026) без запуска Unity: файлы импорта картинок, конфиги вещей
(CosmeticItemConfig) с ценами, скрытие из магазина лишних старых, пул сундука, награды боссов за первую победу,
названия на всех языках. Повторный запуск ничего не дублирует (проверяет, что уже есть).

Раскладка магазина (правило проекта — 10–15 видимых вещей во вкладке, все четыре способа получения):
    видимые — монеты, реклама, покупка и пара вещей «за игру» с подсказкой;
    остальное — только игрой (сундук, уровень, достижения, боссы): HiddenInShop, видно в профиле и в магазине,
    когда уже получено.

Запуск: любым Python 3 (в проекте — Python из Blender):
    blender --background --factory-startup --python Tools/zonk_looks_content.py
"""

import os
import re
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CONTENT = os.path.join(ROOT, "Assets", "ZonkContent")
ITEMS = os.path.join(CONTENT, "Configs", "Cosmetics", "Items")
SPRITES = os.path.join(CONTENT, "Art", "Sprites")
TEXTS = os.path.join(CONTENT, "Localization", "Texts.csv")
CHEST = os.path.join(CONTENT, "Configs", "Game", "Chest.asset")
OPPONENTS = os.path.join(CONTENT, "Configs", "Opponents")

SLOT = {"avatar": "6973b7495d1b75841b5c896b13a875a5", "frame": "1c9650b5ae89c9d4792040d174ae3a9c"}
COINS = "4c4a02ad27a191748b7714f63387dbab"
COMMON, RARE, LEGENDARY, EPIC = 0, 1, 2, 3

# id, редкость, цена: ("coins", n) | ("ads", n) | ("buy",) | ("chest",) | ("boss", имя); показывать в магазине,
# название: ru, en, tr, es, pt, de, fr
AVATARS = [
    ("avatar_cat", RARE, ("coins", 2500), True, ["Кот-шулер", "Card Shark Cat", "Hilebaz Kedi", "Gato tahúr", "Gato trapaceiro", "Falschspieler-Kater", "Chat tricheur"]),
    ("avatar_dog", RARE, ("ads", 4), True, ["Пёс-вышибала", "Bouncer Dog", "Fedai Köpek", "Perro gorila", "Cão segurança", "Türsteher-Hund", "Chien videur"]),
    ("avatar_fox", EPIC, ("buy",), True, ["Лис-аферист", "Con Fox", "Dolandırıcı Tilki", "Zorro estafador", "Raposa trapaceira", "Hochstapler-Fuchs", "Renard escroc"]),
    ("avatar_panda", COMMON, ("chest",), False, ["Панда-громила", "Tough Panda", "Kabadayı Panda", "Panda matón", "Panda brigão", "Raufbold-Panda", "Panda costaud"]),
    ("avatar_hamster", RARE, ("chest",), False, ["Хомяк-богач", "Rich Hamster", "Zengin Hamster", "Hámster rico", "Hamster rico", "Reicher Hamster", "Hamster riche"]),
    ("avatar_owl", RARE, ("ads", 4), True, ["Сова-крупье", "Croupier Owl", "Krupiye Baykuş", "Búho crupier", "Coruja crupiê", "Croupier-Eule", "Hibou croupier"]),
    ("avatar_frog", COMMON, ("coins", 1500), True, ["Жаба-везунчик", "Lucky Frog", "Şanslı Kurbağa", "Rana con suerte", "Sapo sortudo", "Glücksfrosch", "Grenouille chanceuse"]),
    ("avatar_bunny", RARE, ("chest",), False, ["Заяц-фокусник", "Magician Bunny", "Sihirbaz Tavşan", "Conejo mago", "Coelho mágico", "Zauberhase", "Lapin magicien"]),
    ("avatar_skull_fire", LEGENDARY, ("buy",), True, ["Горящий череп", "Flaming Skull", "Alevli Kafatası", "Calavera en llamas", "Caveira em chamas", "Flammenschädel", "Crâne enflammé"]),
    ("avatar_vampire", RARE, ("chest",), False, ["Вампир", "Vampire", "Vampir", "Vampiro", "Vampiro", "Vampir", "Vampire"]),
    ("avatar_zombie", RARE, ("coins", 2500), True, ["Зомби", "Zombie", "Zombi", "Zombi", "Zumbi", "Zombie", "Zombie"]),
    ("avatar_ghost", RARE, ("ads", 5), True, ["Призрак", "Ghost", "Hayalet", "Fantasma", "Fantasma", "Gespenst", "Fantôme"]),
    ("avatar_witch", RARE, ("chest",), False, ["Ведьма", "Witch", "Cadı", "Bruja", "Bruxa", "Hexe", "Sorcière"]),
    ("avatar_demon", EPIC, ("buy",), True, ["Демон", "Demon", "İblis", "Demonio", "Demônio", "Dämon", "Démon"]),
    ("avatar_werewolf", RARE, ("chest",), False, ["Оборотень", "Werewolf", "Kurt Adam", "Hombre lobo", "Lobisomem", "Werwolf", "Loup-garou"]),
    ("avatar_pumpkin", COMMON, ("coins", 1500), True, ["Тыква", "Pumpkin Head", "Bal Kabağı", "Calabaza", "Abóbora", "Kürbiskopf", "Citrouille"]),
    ("avatar_pirate", RARE, ("chest",), False, ["Пират", "Pirate", "Korsan", "Pirata", "Pirata", "Pirat", "Pirate"]),
    ("avatar_cowboy", COMMON, ("coins", 1500), True, ["Ковбой", "Cowboy", "Kovboy", "Vaquero", "Caubói", "Cowboy", "Cow-boy"]),
    ("avatar_gangster", RARE, ("chest",), False, ["Гангстер", "Gangster", "Gangster", "Gánster", "Gângster", "Gangster", "Gangster"]),
    ("avatar_clown", RARE, ("chest",), False, ["Злой клоун", "Evil Clown", "Kötü Palyaço", "Payaso malvado", "Palhaço do mal", "Böser Clown", "Clown maléfique"]),
    ("avatar_granny", RARE, ("chest",), False, ["Бабуля-картёжница", "Card Granny", "Kumarbaz Nine", "Abuela jugadora", "Vovó jogadora", "Kartenoma", "Mamie joueuse"]),
    ("avatar_robot", RARE, ("chest",), False, ["Робот", "Robot", "Robot", "Robot", "Robô", "Roboter", "Robot"]),
    ("avatar_ninja", RARE, ("ads", 5), True, ["Ниндзя", "Ninja", "Ninja", "Ninja", "Ninja", "Ninja", "Ninja"]),
    ("avatar_knight", RARE, ("chest",), False, ["Рыцарь", "Knight", "Şövalye", "Caballero", "Cavaleiro", "Ritter", "Chevalier"]),
    ("avatar_dice_guy", COMMON, ("chest",), False, ["Кубик", "Dice Dude", "Zar Adam", "Señor Dado", "Senhor Dado", "Würfelkerl", "Monsieur Dé"]),
    ("avatar_chip_guy", COMMON, ("chest",), False, ["Фишка", "Chip Dude", "Fiş Adam", "Señor Ficha", "Senhor Ficha", "Chipkerl", "Monsieur Jeton"]),
    ("avatar_ace", RARE, ("chest",), False, ["Туз пик", "Ace of Spades", "Maça Ası", "As de picas", "Ás de espadas", "Pik-Ass", "As de pique"]),
    ("avatar_joker", LEGENDARY, ("chest",), False, ["Джокер", "Joker", "Joker", "Comodín", "Curinga", "Joker", "Joker"]),
    ("avatar_moneybag", RARE, ("chest",), False, ["Мешок денег", "Money Bag", "Para Kesesi", "Saco de dinero", "Saco de dinheiro", "Geldsack", "Sac d'argent"]),
    ("avatar_lucky7", LEGENDARY, ("chest",), False, ["Счастливая семёрка", "Lucky Seven", "Şanslı Yedi", "Siete de la suerte", "Sete da sorte", "Glückssieben", "Sept chanceux"]),
]
BOSS_NAMES = {
    "captain": ["Капитан", "Captain", "Kaptan", "Capitán", "Capitão", "Kapitän", "Capitaine"],
    "claw": ["Мадам Клешня", "Madam Claw", "Madam Kıskaç", "Madame Pinza", "Madame Pinça", "Madame Schere", "Madame Pince"],
    "krot": ["Крот", "Mole", "Köstebek", "Topo", "Toupeira", "Maulwurf", "Taupe"],
    "madame": ["Мадам Полночь", "Madame Midnight", "Madam Gece Yarısı", "Madame Medianoche", "Madame Meia-Noite", "Madame Mitternacht", "Madame Minuit"],
    "rose": ["Графиня Роза", "Countess Rose", "Kontes Gül", "Condesa Rosa", "Condessa Rosa", "Gräfin Rose", "Comtesse Rose"],
    "sych": ["Сыч", "Owlet", "Baykuş", "Mochuelo", "Mocho", "Kauz", "Chevêche"],
    "tikhon": ["Тихон", "Tikhon", "Tihon", "Tijon", "Tikhon", "Tichon", "Tikhon"],
    "agafya": ["Агафья", "Agafya", "Agafya", "Agafia", "Agáfia", "Agafja", "Agafia"],
    "bo": ["Бо", "Bo", "Bo", "Bo", "Bo", "Bo", "Bo"],
    "fortuna": ["Фортуна", "Fortuna", "Fortuna", "Fortuna", "Fortuna", "Fortuna", "Fortuna"],
}
SHOWN_BOSSES = ("captain", "fortuna")
for boss, names in BOSS_NAMES.items():
    AVATARS.append(("avatar_boss_" + boss, EPIC, ("boss", boss), boss in SHOWN_BOSSES,
                    ["Босс: " + names[0], "Boss: " + names[1], "Patron: " + names[2], "Jefe: " + names[3], "Chefe: " + names[4],
                     "Boss: " + names[5], "Boss : " + names[6]]))

FRAMES = [
    ("frame_cat_ears", RARE, ("coins", 2000), True, ["Кошачьи ушки", "Cat Ears", "Kedi Kulakları", "Orejas de gato", "Orelhas de gato", "Katzenohren", "Oreilles de chat"]),
    ("frame_hearts", COMMON, ("coins", 1500), True, ["Сердечки", "Hearts", "Kalpler", "Corazones", "Corações", "Herzen", "Cœurs"]),
    ("frame_flowers", COMMON, ("chest",), False, ["Цветы", "Flowers", "Çiçekler", "Flores", "Flores", "Blumen", "Fleurs"]),
    ("frame_horns", EPIC, ("buy",), True, ["Рога демона", "Demon Horns", "İblis Boynuzları", "Cuernos de demonio", "Chifres de demônio", "Dämonenhörner", "Cornes de démon"]),
    ("frame_web", RARE, ("ads", 4), True, ["Паутина", "Spider Web", "Örümcek Ağı", "Telaraña", "Teia de aranha", "Spinnennetz", "Toile d'araignée"]),
    ("frame_bones", RARE, ("ads", 4), True, ["Кости", "Bones", "Kemikler", "Huesos", "Ossos", "Knochen", "Os"]),
    ("frame_tentacles", RARE, ("chest",), False, ["Щупальца", "Tentacles", "Dokunaçlar", "Tentáculos", "Tentáculos", "Tentakel", "Tentacules"]),
    ("frame_slime", RARE, ("coins", 2500), True, ["Слизь", "Slime", "Balçık", "Baba", "Gosma", "Schleim", "Bave"]),
    ("frame_crown_laurel", LEGENDARY, ("buy",), True, ["Корона и лавр", "Crown and Laurel", "Taç ve Defne", "Corona y laurel", "Coroa e louros", "Krone und Lorbeer", "Couronne et laurier"]),
    ("frame_lightning", RARE, ("ads", 5), True, ["Молнии", "Lightning", "Şimşekler", "Relámpagos", "Raios", "Blitze", "Éclairs"]),
    ("frame_ice", RARE, ("chest",), False, ["Лёд", "Ice", "Buz", "Hielo", "Gelo", "Eis", "Glace"]),
    ("frame_neon", EPIC, ("buy",), True, ["Неон", "Neon", "Neon", "Neón", "Neon", "Neon", "Néon"]),
    ("frame_pirate", RARE, ("coins", 2500), True, ["Пиратская", "Pirate", "Korsan", "Pirata", "Pirata", "Piraten", "Pirate"]),
    ("frame_cards", RARE, ("chest",), False, ["Карты", "Cards", "Kartlar", "Cartas", "Cartas", "Karten", "Cartes"]),
]

# В сундуке только обычные, редкие и легендарные: строк выпадения эпических в Chest.asset нет — эпическая бы не выпала.
# Старые: в магазине остаются только эти (путь «за игру» с подсказкой), остальные — только игрой
OLD_SHOWN = {"avatar_crown", "frame_gold", "frame_royal", "frame_fire"}


def guid():
    return uuid.uuid4().hex


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})", read(path + ".meta"), re.M)
    return m.group(1)


def ensure_sprite_meta(png, template):
    if os.path.exists(png + ".meta"):
        return meta_guid(png)
    g = guid()
    text = re.sub(r"^guid: [0-9a-f]{32}", "guid: " + g, read(template + ".meta"), count=1, flags=re.M)
    write(png + ".meta", text)
    return g


def price_yaml(price, item_id, rid):
    kind = price[0]
    if kind == "coins":
        return ("    - rid: %d\n      type: {class: CurrencyPriceOption, ns: Zonk.Configs, asm: Zonk}\n      data:\n"
                "        Currency: {fileID: 11400000, guid: %s, type: 2}\n        Amount: %d\n" % (rid, COINS, price[1]))
    if kind == "ads":
        return ("    - rid: %d\n      type: {class: RewardedAdPriceOption, ns: Zonk.Configs, asm: Zonk}\n      data:\n"
                "        AdsRequired: %d\n" % (rid, price[1]))
    if kind == "buy":
        return ("    - rid: %d\n      type: {class: PurchasePriceOption, ns: Zonk.Configs, asm: Zonk}\n      data:\n"
                "        ProductId: %s\n" % (rid, item_id))
    hint = "hint.chest" if kind == "chest" else "hint.beat_" + price[1]
    return ("    - rid: %d\n      type: {class: ProgressPriceOption, ns: Zonk.Configs, asm: Zonk}\n      data:\n"
            "        HintKey: %s\n" % (rid, hint))


def item_yaml(item_id, slot, sprite_guid, order, rarity, price, hidden, rid_base):
    return (
        "%%YAML 1.1\n%%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: 3758024ff1cf4ad4caa14cc6c86d79b3, type: 3}\n"
        "  m_Name: %(id)s\n  m_EditorClassIdentifier: Zonk::Zonk.Configs.CosmeticItemConfig\n"
        "  Id: %(id)s\n  NameKey: item.%(id)s\n  Slot: {fileID: 11400000, guid: %(slot)s, type: 2}\n"
        "  Icon: {fileID: 21300000, guid: %(sprite)s, type: 3}\n  Order: %(order)d\n  Rarity: %(rarity)d\n"
        "  Payload:\n    rid: %(r1)d\n  Price:\n    Options:\n    - rid: %(r2)d\n  HiddenInShop: %(hidden)d\n"
        "  ShopTab: {fileID: 0}\n  references:\n    version: 2\n    RefIds:\n"
        "    - rid: %(r1)d\n      type: {class: SpritePayload, ns: Zonk.Configs, asm: Zonk}\n      data:\n"
        "        Sprite: {fileID: 21300000, guid: %(sprite)s, type: 3}\n"
        % dict(id=item_id, slot=SLOT[slot], sprite=sprite_guid, order=order, rarity=rarity, r1=rid_base, r2=rid_base + 1,
               hidden=1 if hidden else 0)
    ) + price_yaml(price, item_id, rid_base + 1)


def asset_meta(g):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % g)


def create_items(entries, slot, folder, template_png, order_start):
    created, guids = 0, {}
    for n, (item_id, rarity, price, shown, names) in enumerate(entries):
        png = os.path.join(SPRITES, folder, item_id + ".png")
        if not os.path.exists(png):
            raise SystemExit("нет картинки " + png)
        sprite = ensure_sprite_meta(png, template_png)
        path = os.path.join(ITEMS, item_id + ".asset")
        if not os.path.exists(path):
            write(path, item_yaml(item_id, slot, sprite, order_start + n, rarity, price, not shown,
                                  7700000000000000000 + (order_start + n) * 10))
            write(path + ".meta", asset_meta(guid()))
            created += 1
        guids[item_id] = meta_guid(path)
    return created, guids


def hide_old(prefix, keep):
    changed = 0
    for name in os.listdir(ITEMS):
        if not (name.startswith(prefix) and name.endswith(".asset")):
            continue
        item_id = name[:-6]
        if item_id in keep or item_id in NEW_IDS:
            continue
        path = os.path.join(ITEMS, name)
        text = read(path)
        if "  HiddenInShop: 0" in text:
            write(path, text.replace("  HiddenInShop: 0", "  HiddenInShop: 1", 1))
            changed += 1
    return changed


def show_slots():
    for slot in ("avatar", "frame"):
        path = os.path.join(CONTENT, "Configs", "Cosmetics", "Slots", "Slot_%s.asset" % slot)
        text = read(path)
        if "  ShowInShop: 0" in text:
            write(path, text.replace("  ShowInShop: 0", "  ShowInShop: 1", 1))


def add_to_chest(guids):
    text = read(CHEST)
    lines = text.split("\n")
    start = next(i for i, l in enumerate(lines) if l.startswith("  Items:"))
    end = start + 1
    while end < len(lines) and lines[end].startswith("  - {fileID"):
        end += 1
    added = 0
    for g in guids:
        if g in text:
            continue
        lines.insert(end, "  - {fileID: 11400000, guid: %s, type: 2}" % g)
        end += 1
        added += 1
    write(CHEST, "\n".join(lines))
    return added


def add_boss_rewards(guids):
    added = 0
    for boss in BOSS_NAMES:
        path = os.path.join(OPPONENTS, "Opp_%s.asset" % boss)
        g = guids["avatar_boss_" + boss]
        text = read(path)
        if g in text:
            continue
        rid = 7790000000000000000 + list(BOSS_NAMES).index(boss)
        lines = text.split("\n")
        i = next(k for k, l in enumerate(lines) if l.startswith("  FirstWinRewards:"))
        j = i + 1
        while j < len(lines) and lines[j].startswith("  - rid:"):
            j += 1
        lines.insert(j, "  - rid: %d" % rid)
        k = next(k for k, l in enumerate(lines) if l.startswith("    RefIds:"))
        lines[k + 1:k + 1] = ["    - rid: %d" % rid, "      type: {class: ContentReward, ns: Zonk.Configs, asm: Zonk}",
                              "      data:", "        Item: {fileID: 11400000, guid: %s, type: 2}" % g]
        write(path, "\n".join(lines))
        added += 1
    return added


def add_texts(entries):
    text = read(TEXTS)
    newline = "\r\n" if "\r\n" in text else "\n"
    rows = []
    for item_id, _, _, _, names in entries:
        key = "item." + item_id
        if re.search(r"^" + re.escape(key) + ",", text, re.M):
            continue
        rows.append(key + "," + ",".join('"%s"' % n.replace('"', '""') for n in names))
    if rows:
        if not text.endswith(newline):
            text += newline
        text += newline.join(rows) + newline
        write(TEXTS, text)
    return len(rows)


NEW_IDS = {e[0] for e in AVATARS + FRAMES}


def main():
    a_created, a_guids = create_items(AVATARS, "avatar", "Avatars", os.path.join(SPRITES, "Avatars", "avatar_star.png"), 100)
    f_created, f_guids = create_items(FRAMES, "frame", "Frames", os.path.join(SPRITES, "Frames", "frame_gold.png"), 100)
    hidden = hide_old("avatar_", OLD_SHOWN) + hide_old("frame_", OLD_SHOWN)
    show_slots()
    chest = [a_guids[e[0]] for e in AVATARS if e[2][0] == "chest"] + [f_guids[e[0]] for e in FRAMES if e[2][0] == "chest"]
    print("[looks] avatars created %d, frames created %d, old hidden %d, chest +%d, boss rewards +%d, texts +%d" % (
        a_created, f_created, hidden, add_to_chest(chest), add_boss_rewards(a_guids), add_texts(AVATARS + FRAMES)))


main()

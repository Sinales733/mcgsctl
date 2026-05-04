import com.healthmarketscience.jackcess.Column;
import com.healthmarketscience.jackcess.Database;
import com.healthmarketscience.jackcess.DatabaseBuilder;
import com.healthmarketscience.jackcess.Row;
import com.healthmarketscience.jackcess.Table;

import java.io.File;
import java.io.Writer;
import java.security.MessageDigest;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.Date;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

public final class MceExport {
  private static final Charset MCGS_CHARSET = Charset.forName("GBK");
  private static final Charset MOJIBAKE_CHARSET = Charset.forName("GB18030");
  private static final DateTimeFormatter ISO =
      DateTimeFormatter.ISO_OFFSET_DATE_TIME.withZone(ZoneOffset.UTC);

  public static void main(String[] args) throws Exception {
    if (args.length < 2) {
      System.err.println("usage: MceExport <project.mce> <outdir>");
      System.exit(2);
    }

    File dbFile = new File(args[0]);
    Path outDir = Path.of(args[1]);
    Files.createDirectories(outDir);

    try (Database db = new DatabaseBuilder(dbFile)
        .setReadOnly(true)
        .setCharset(MCGS_CHARSET)
        .open()) {
      writeSummary(db, dbFile, outDir.resolve("summary.json"));
      writeSchema(db, outDir.resolve("schema.json"));
      writeDataRows(db, outDir.resolve("data.json"));
      writeBlobStrings(db, outDir.resolve("blob_strings.json"));
      writeBlobGeometry(db, outDir.resolve("blob_geometry.json"));
    }
  }

  private static void writeSummary(Database db, File dbFile, Path file) throws Exception {
    try (Writer w = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
      w.write("{\n");
      prop(w, "project", dbFile.getAbsolutePath(), true, 1);
      prop(w, "format", String.valueOf(db.getFileFormat()), true, 1);
      w.write(indent(1) + "\"tables\": [\n");
      int index = 0;
      for (String tableName : db.getTableNames()) {
        Table table = db.getTable(tableName);
        if (index++ > 0) w.write(",\n");
        w.write(indent(2) + "{");
        inlineProp(w, "name", tableName, true);
        inlineProp(w, "rows", table.getRowCount(), true);
        inlineProp(w, "columns", table.getColumns().size(), false);
        w.write("}");
      }
      w.write("\n" + indent(1) + "]\n");
      w.write("}\n");
    }
  }

  private static void writeSchema(Database db, Path file) throws Exception {
    try (Writer w = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
      w.write("{\n");
      int tableIndex = 0;
      for (String tableName : db.getTableNames()) {
        if (tableIndex++ > 0) w.write(",\n");
        Table table = db.getTable(tableName);
        w.write(indent(1) + quote(tableName) + ": [\n");
        int columnIndex = 0;
        for (Column c : table.getColumns()) {
          if (columnIndex++ > 0) w.write(",\n");
          w.write(indent(2) + "{");
          inlineProp(w, "name", c.getName(), true);
          inlineProp(w, "type", String.valueOf(c.getType()), true);
          inlineProp(w, "length", c.getLength(), false);
          w.write("}");
        }
        w.write("\n" + indent(1) + "]");
      }
      w.write("\n}\n");
    }
  }

  private static void writeDataRows(Database db, Path file) throws Exception {
    try (Writer w = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
      Table data = db.getTable("Data");
      w.write("[\n");
      if (data != null) {
        int rowIndex = 0;
        for (Row row : data) {
          if (rowIndex++ > 0) w.write(",\n");
          w.write(indent(1) + "{\n");
          int columnIndex = 0;
          for (Column c : data.getColumns()) {
            if (columnIndex++ > 0) w.write(",\n");
            w.write(indent(2) + quote(c.getName()) + ": ");
            writeValue(w, row.get(c.getName()));
          }
          w.write("\n" + indent(1) + "}");
        }
      }
      w.write("\n]\n");
    }
  }

  private static void writeBlobStrings(Database db, Path file) throws Exception {
    try (Writer w = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
      w.write("[\n");
      int entryIndex = 0;
      for (String tableName : db.getTableNames()) {
        Table table = db.getTable(tableName);
        for (Row row : table) {
          String rowKey = firstCell(row);
          String rowLabel = firstString(row);
          for (Column c : table.getColumns()) {
            Object value = row.get(c.getName());
            if (!(value instanceof byte[] bytes)) continue;
            List<String> strings = extractStrings(bytes);
            if (entryIndex++ > 0) w.write(",\n");
            w.write(indent(1) + "{\n");
            prop(w, "table", tableName, true, 2);
            prop(w, "column", c.getName(), true, 2);
            prop(w, "rowKey", rowKey, true, 2);
            prop(w, "rowLabel", rowLabel, true, 2);
            prop(w, "bytes", bytes.length, true, 2);
            w.write(indent(2) + "\"strings\": [");
            for (int i = 0; i < strings.size(); i++) {
              if (i > 0) w.write(", ");
              w.write(quote(strings.get(i)));
            }
            w.write("]\n");
            w.write(indent(1) + "}");
          }
        }
      }
      w.write("\n]\n");
    }
  }

  private static void writeBlobGeometry(Database db, Path file) throws Exception {
    try (Writer w = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
      w.write("[\n");
      int entryIndex = 0;
      for (String tableName : db.getTableNames()) {
        Table table = db.getTable(tableName);
        for (Row row : table) {
          String rowKey = firstCell(row);
          String rowLabel = firstString(row);
          for (Column c : table.getColumns()) {
            Object value = row.get(c.getName());
            if (!(value instanceof byte[] bytes)) continue;
            List<ClassOccurrence> classes = findClassOccurrences(bytes);
            if (classes.isEmpty()) continue;
            List<RectCandidate> rects = findRectCandidates(bytes);
            List<TextAnchor> anchors = findTextAnchors(bytes);
            if (!isLikelyCanvasObjectBlob(tableName, c.getName())) continue;
            if (entryIndex++ > 0) w.write(",\n");
            w.write(indent(1) + "{\n");
            prop(w, "table", tableName, true, 2);
            prop(w, "column", c.getName(), true, 2);
            prop(w, "rowKey", rowKey, true, 2);
            prop(w, "rowLabelLength", rowLabel.length(), true, 2);
            prop(w, "rowLabelSha256", sha256Hex(rowLabel.getBytes(StandardCharsets.UTF_8)), true, 2);
            prop(w, "bytes", bytes.length, true, 2);
            prop(w, "sha256", sha256Hex(bytes), true, 2);
            w.write(indent(2) + "\"classOccurrences\": [");
            for (int i = 0; i < classes.size(); i++) {
              if (i > 0) w.write(", ");
              ClassOccurrence cls = classes.get(i);
              w.write("{");
              inlineProp(w, "offset", hex(cls.offset), true);
              inlineProp(w, "className", cls.className, false);
              w.write("}");
            }
            w.write("],\n");
            w.write(indent(2) + "\"candidateRectangles\": [");
            for (int i = 0; i < rects.size(); i++) {
              if (i > 0) w.write(", ");
              RectCandidate r = rects.get(i);
              w.write("{");
              inlineProp(w, "offset", hex(r.offset), true);
              inlineProp(w, "encoding", r.encoding, true);
              inlineProp(w, "pattern", r.pattern, true);
              inlineProp(w, "x", r.x, true);
              inlineProp(w, "y", r.y, true);
              inlineProp(w, "width", r.width, true);
              inlineProp(w, "height", r.height, false);
              w.write("}");
            }
            w.write("],\n");
            w.write(indent(2) + "\"textAnchors\": [");
            for (int i = 0; i < anchors.size(); i++) {
              if (i > 0) w.write(", ");
              TextAnchor a = anchors.get(i);
              w.write("{");
              inlineProp(w, "offset", hex(a.offset), true);
              inlineProp(w, "encoding", a.encoding, true);
              inlineProp(w, "byteLength", a.byteLength, true);
              inlineProp(w, "charLength", a.charLength, true);
              inlineProp(w, "sha256", a.sha256, true);
              w.write(quote("sample") + ": ");
              if (a.sample == null) w.write("null");
              else w.write(quote(a.sample));
              w.write("}");
            }
            w.write("]\n");
            w.write(indent(1) + "}");
          }
        }
      }
      w.write("\n]\n");
    }
  }

  private static boolean isLikelyCanvasObjectBlob(String tableName, String columnName) {
    if (!"lbObjects".equalsIgnoreCase(columnName)) return false;
    return "WndUser".equalsIgnoreCase(tableName) || "WndDevice".equalsIgnoreCase(tableName);
  }

  private static List<ClassOccurrence> findClassOccurrences(byte[] bytes) {
    List<ClassOccurrence> found = new ArrayList<>();
    byte[] prefix = "CDraw".getBytes(StandardCharsets.US_ASCII);
    for (int i = 0; i + prefix.length < bytes.length && found.size() < 200; i++) {
      boolean match = true;
      for (int j = 0; j < prefix.length; j++) {
        if (bytes[i + j] != prefix[j]) {
          match = false;
          break;
        }
      }
      if (!match) continue;
      int end = i;
      while (end < bytes.length && isAsciiNameByte(bytes[end])) end++;
      if (end > i) {
        found.add(new ClassOccurrence(i, new String(bytes, i, end - i, StandardCharsets.US_ASCII)));
        i = end;
      }
    }
    return found;
  }

  private static List<TextAnchor> findTextAnchors(byte[] bytes) throws Exception {
    List<TextAnchor> found = new ArrayList<>();
    Set<String> seen = new LinkedHashSet<>();
    for (int i = 0; i < bytes.length && found.size() < 400; ) {
      if (!isAsciiTextByte(bytes[i])) {
        i++;
        continue;
      }
      int start = i;
      while (i < bytes.length && isAsciiTextByte(bytes[i])) i++;
      if (i - start >= 3) {
        String text = new String(bytes, start, i - start, StandardCharsets.US_ASCII);
        addTextAnchor(found, seen, start, "ascii", i - start, text);
      }
    }

    for (int i = 0; i < bytes.length && found.size() < 800; ) {
      if (!isGbkTextStart(bytes, i)) {
        i++;
        continue;
      }
      int start = i;
      while (i < bytes.length && i - start < 320) {
        if (isAsciiTextByte(bytes[i])) {
          i++;
          continue;
        }
        if (isGbkDoubleByte(bytes, i)) {
          i += 2;
          continue;
        }
        break;
      }
      if (i - start >= 4) {
        String text = new String(bytes, start, i - start, MCGS_CHARSET);
        addTextAnchor(found, seen, start, "gbk", i - start, text);
      }
      if (i == start) i++;
    }

    for (int i = 0; i + 5 < bytes.length && found.size() < 400; ) {
      if (!isUtf16LeUseful(bytes, i)) {
        i += 2;
        continue;
      }
      int start = i;
      while (i + 1 < bytes.length && isUtf16LeUseful(bytes, i)) i += 2;
      if (i - start >= 6) {
        String text = new String(bytes, start, i - start, StandardCharsets.UTF_16LE);
        addTextAnchor(found, seen, start, "utf16le", i - start, text);
      }
    }
    return found;
  }

  private static void addTextAnchor(List<TextAnchor> found, Set<String> seen, int offset,
                                    String encoding, int byteLength, String rawText) throws Exception {
    String text = normalizeText(rawText).trim();
    if (text.length() < 3 || text.indexOf('\uFFFD') >= 0) return;
    if (!looksMeaningful(text)) return;
    String key = encoding + ":" + offset + ":" + text;
    if (!seen.add(key)) return;
    String sample = isSafeEvidenceSample(text) ? text : null;
    found.add(new TextAnchor(offset, encoding, byteLength, text.length(),
        sha256Hex(text.getBytes(StandardCharsets.UTF_8)), sample));
  }

  private static boolean isAsciiTextByte(byte value) {
    int b = value & 0xFF;
    return b >= 0x20 && b <= 0x7E;
  }

  private static boolean isGbkTextStart(byte[] bytes, int offset) {
    if (offset >= bytes.length) return false;
    if (isAsciiTextByte(bytes[offset])) return true;
    return isGbkDoubleByte(bytes, offset);
  }

  private static boolean isGbkDoubleByte(byte[] bytes, int offset) {
    if (offset + 1 >= bytes.length) return false;
    int b1 = bytes[offset] & 0xFF;
    int b2 = bytes[offset + 1] & 0xFF;
    return b1 >= 0x81 && b1 <= 0xFE && b2 >= 0x40 && b2 <= 0xFE && b2 != 0x7F;
  }

  private static boolean isUtf16LeUseful(byte[] bytes, int offset) {
    if (offset + 1 >= bytes.length) return false;
    int low = bytes[offset] & 0xFF;
    int high = bytes[offset + 1] & 0xFF;
    if (high == 0 && low >= 0x20 && low <= 0x7E) return true;
    char ch = (char) (low | (high << 8));
    return isUsefulChar(ch);
  }

  private static boolean isSafeEvidenceSample(String text) {
    if (text.length() > 160) return false;
    for (int i = 0; i < text.length(); i++) {
      char ch = text.charAt(i);
      if (ch == '\uFFFD') return false;
      Character.UnicodeBlock block = Character.UnicodeBlock.of(ch);
      if (block == Character.UnicodeBlock.PRIVATE_USE_AREA
          || block == Character.UnicodeBlock.SPECIALS
          || block == Character.UnicodeBlock.LOW_SURROGATES
          || block == Character.UnicodeBlock.HIGH_SURROGATES) return false;
      if (Character.isISOControl(ch) || Character.isSurrogate(ch)) return false;
      if (Character.isLetterOrDigit(ch) || Character.isWhitespace(ch)) continue;
      if ("_./:#()[]{}=+-*@,;，。：；（）【】#".indexOf(ch) >= 0) continue;
      if (isCjk(ch)) continue;
      if (block == Character.UnicodeBlock.CJK_SYMBOLS_AND_PUNCTUATION
          || block == Character.UnicodeBlock.HALFWIDTH_AND_FULLWIDTH_FORMS
          || block == Character.UnicodeBlock.GENERAL_PUNCTUATION) continue;
      return false;
    }
    return true;
  }

  private static boolean isAsciiNameByte(byte value) {
    int b = value & 0xFF;
    return (b >= 'A' && b <= 'Z')
        || (b >= 'a' && b <= 'z')
        || (b >= '0' && b <= '9')
        || b == '_';
  }

  private static List<RectCandidate> findRectCandidates(byte[] bytes) {
    List<RectCandidate> found = new ArrayList<>();
    Set<String> seen = new LinkedHashSet<>();
    for (int offset = 0; offset + 16 <= bytes.length && found.size() < 20000; offset++) {
      int a = int32(bytes, offset);
      int b = int32(bytes, offset + 4);
      int c = int32(bytes, offset + 8);
      int d = int32(bytes, offset + 12);
      addRectCandidate(found, seen, offset, "int32", "xywh", a, b, c, d);
      addRectCandidate(found, seen, offset, "int32", "ltrb", a, b, c - a, d - b);
    }
    for (int offset = 0; offset + 8 <= bytes.length && found.size() < 20000; offset += 2) {
      int a = int16(bytes, offset);
      int b = int16(bytes, offset + 2);
      int c = int16(bytes, offset + 4);
      int d = int16(bytes, offset + 6);
      addRectCandidate(found, seen, offset, "int16", "xywh", a, b, c, d);
      addRectCandidate(found, seen, offset, "int16", "ltrb", a, b, c - a, d - b);
    }
    for (int offset = 0; offset + 16 <= bytes.length && found.size() < 20000; offset += 4) {
      Float a = float32(bytes, offset);
      Float b = float32(bytes, offset + 4);
      Float c = float32(bytes, offset + 8);
      Float d = float32(bytes, offset + 12);
      addFloatRectCandidate(found, seen, offset, "float32", "xywh", a, b, c, d);
      if (a != null && b != null && c != null && d != null) {
        addFloatRectCandidate(found, seen, offset, "float32", "ltrb", a, b, c - a, d - b);
      }
    }
    for (int offset = 0; offset + 32 <= bytes.length && found.size() < 20000; offset += 8) {
      Double a = float64(bytes, offset);
      Double b = float64(bytes, offset + 8);
      Double c = float64(bytes, offset + 16);
      Double d = float64(bytes, offset + 24);
      addDoubleRectCandidate(found, seen, offset, "float64", "xywh", a, b, c, d);
      if (a != null && b != null && c != null && d != null) {
        addDoubleRectCandidate(found, seen, offset, "float64", "ltrb", a, b, c - a, d - b);
      }
    }
    return found;
  }

  private static void addRectCandidate(List<RectCandidate> found, Set<String> seen, int offset,
                                       String encoding, String pattern, int x, int y, int width, int height) {
    if (width < 8 || height < 8) return;
    if (width > 2200 || height > 1800) return;
    if (x < -200 || y < -200 || x > 2200 || y > 1800) return;
    if (x + width > 2400 || y + height > 2000) return;
    String key = x + ":" + y + ":" + width + ":" + height;
    if (!seen.add(key)) return;
    found.add(new RectCandidate(offset, encoding, pattern, x, y, width, height));
  }

  private static void addFloatRectCandidate(List<RectCandidate> found, Set<String> seen, int offset,
                                            String encoding, String pattern, Float x, Float y, Float width, Float height) {
    if (x == null || y == null || width == null || height == null) return;
    if (!isNearInteger(x) || !isNearInteger(y) || !isNearInteger(width) || !isNearInteger(height)) return;
    addRectCandidate(found, seen, offset, encoding, pattern, Math.round(x), Math.round(y), Math.round(width), Math.round(height));
  }

  private static void addDoubleRectCandidate(List<RectCandidate> found, Set<String> seen, int offset,
                                             String encoding, String pattern, Double x, Double y, Double width, Double height) {
    if (x == null || y == null || width == null || height == null) return;
    if (!isNearInteger(x) || !isNearInteger(y) || !isNearInteger(width) || !isNearInteger(height)) return;
    addRectCandidate(found, seen, offset, encoding, pattern, (int) Math.round(x), (int) Math.round(y),
        (int) Math.round(width), (int) Math.round(height));
  }

  private static boolean isNearInteger(double value) {
    if (!Double.isFinite(value)) return false;
    if (value < -200 || value > 2400) return false;
    return Math.abs(value - Math.rint(value)) <= 0.05;
  }

  private static int int16(byte[] bytes, int offset) {
    int value = (bytes[offset] & 0xFF) | (bytes[offset + 1] << 8);
    return (short) value;
  }

  private static int int32(byte[] bytes, int offset) {
    return (bytes[offset] & 0xFF)
        | ((bytes[offset + 1] & 0xFF) << 8)
        | ((bytes[offset + 2] & 0xFF) << 16)
        | (bytes[offset + 3] << 24);
  }

  private static Float float32(byte[] bytes, int offset) {
    int bits = int32(bytes, offset);
    float value = Float.intBitsToFloat(bits);
    return Float.isFinite(value) ? value : null;
  }

  private static Double float64(byte[] bytes, int offset) {
    long bits = 0;
    for (int i = 7; i >= 0; i--) {
      bits = (bits << 8) | (bytes[offset + i] & 0xFFL);
    }
    double value = Double.longBitsToDouble(bits);
    return Double.isFinite(value) ? value : null;
  }

  private static String sha256Hex(byte[] bytes) throws Exception {
    MessageDigest digest = MessageDigest.getInstance("SHA-256");
    byte[] hash = digest.digest(bytes);
    StringBuilder sb = new StringBuilder(hash.length * 2);
    for (byte b : hash) sb.append(String.format("%02x", b & 0xFF));
    return sb.toString();
  }

  private static String hex(int value) {
    return "0x" + Integer.toHexString(value).toUpperCase();
  }

  private record ClassOccurrence(int offset, String className) {}

  private record RectCandidate(int offset, String encoding, String pattern, int x, int y, int width, int height) {}

  private record TextAnchor(int offset, String encoding, int byteLength, int charLength, String sha256, String sample) {}

  private static void writeValue(Writer w, Object value) throws Exception {
    if (value == null) {
      w.write("null");
    } else if (value instanceof Number || value instanceof Boolean) {
      w.write(String.valueOf(value));
    } else if (value instanceof Date date) {
      w.write(quote(ISO.format(date.toInstant())));
    } else if (value instanceof byte[] bytes) {
      w.write("{");
      inlineProp(w, "blobBytes", bytes.length, true);
      w.write("\"strings\": [");
      List<String> strings = extractStrings(bytes);
      for (int i = 0; i < strings.size(); i++) {
        if (i > 0) w.write(", ");
        w.write(quote(strings.get(i)));
      }
      w.write("]}");
    } else {
      w.write(quote(normalizeText(String.valueOf(value))));
    }
  }

  private static List<String> extractStrings(byte[] bytes) {
    Set<String> found = new LinkedHashSet<>();
    extractDecoded(found, new String(bytes, MCGS_CHARSET));
    extractDecoded(found, new String(bytes, StandardCharsets.UTF_16LE));
    extractDecoded(found, new String(bytes, StandardCharsets.UTF_8));
    List<String> result = new ArrayList<>();
    for (String s : found) {
      if (s.length() > 200) s = s.substring(0, 200);
      result.add(s);
      if (result.size() >= 300) break;
    }
    return result;
  }

  private static void extractDecoded(Set<String> found, String decoded) {
    StringBuilder run = new StringBuilder();
    for (int i = 0; i < decoded.length(); i++) {
      char ch = decoded.charAt(i);
      if (isUsefulChar(ch)) {
        run.append(ch);
      } else {
        flushRun(found, run);
      }
    }
    flushRun(found, run);
  }

  private static void flushRun(Set<String> found, StringBuilder run) {
    if (run.length() < 3) {
      run.setLength(0);
      return;
    }
    String text = run.toString().trim();
    run.setLength(0);
    if (text.length() < 3 || text.indexOf('\uFFFD') >= 0) return;
    if (!looksMeaningful(text)) return;
    found.add(text);
  }

  private static boolean isUsefulChar(char ch) {
    if (ch == '\uFFFD' || Character.isISOControl(ch)) return false;
    if (ch >= 0x20 && ch <= 0x7E) return true;
    Character.UnicodeBlock block = Character.UnicodeBlock.of(ch);
    return block == Character.UnicodeBlock.CJK_UNIFIED_IDEOGRAPHS
        || block == Character.UnicodeBlock.CJK_UNIFIED_IDEOGRAPHS_EXTENSION_A
        || block == Character.UnicodeBlock.CJK_SYMBOLS_AND_PUNCTUATION
        || block == Character.UnicodeBlock.HALFWIDTH_AND_FULLWIDTH_FORMS
        || block == Character.UnicodeBlock.GENERAL_PUNCTUATION;
  }

  private static boolean looksMeaningful(String text) {
    int useful = 0;
    for (int i = 0; i < text.length(); i++) {
      char ch = text.charAt(i);
      if (Character.isLetterOrDigit(ch) || isCjk(ch)) useful++;
    }
    return useful >= Math.min(3, text.length());
  }

  private static boolean isCjk(char ch) {
    Character.UnicodeBlock block = Character.UnicodeBlock.of(ch);
    return block == Character.UnicodeBlock.CJK_UNIFIED_IDEOGRAPHS
        || block == Character.UnicodeBlock.CJK_UNIFIED_IDEOGRAPHS_EXTENSION_A;
  }

  private static String firstCell(Row row) {
    for (Object value : row.values()) {
      if (value != null) return String.valueOf(value);
    }
    return "";
  }

  private static String firstString(Row row) {
    for (Object value : row.values()) {
      if (value instanceof String s && !s.isBlank()) return normalizeText(s);
    }
    return "";
  }

  private static String normalizeText(String text) {
    if (text.isEmpty()) return text;
    String repaired = new String(text.getBytes(MOJIBAKE_CHARSET), StandardCharsets.UTF_8);
    if (containsMojibakeMarker(text) && repaired.indexOf('\uFFFD') < 0 && textScore(repaired) > 0) return repaired;
    if (textScore(repaired) > textScore(text) + 4) return repaired;
    return text;
  }

  private static boolean containsMojibakeMarker(String text) {
    String markers = "绯荤粺鍐呭缓鏁版嵁瀵硅薄閫氳瘽鍙傛暟鍚庡彴";
    for (int i = 0; i < text.length(); i++) {
      if (markers.indexOf(text.charAt(i)) >= 0) return true;
    }
    return false;
  }

  private static int textScore(String text) {
    int score = 0;
    for (int i = 0; i < text.length(); i++) {
      char ch = text.charAt(i);
      if (ch == '\uFFFD') score -= 4;
      else if (isCjk(ch)) score += 2;
      else if (Character.isLetterOrDigit(ch)) score += 1;
      else if (ch == '_' || ch == '.' || ch == '-' || Character.isWhitespace(ch)) score += 0;
      else score -= 1;
    }
    return score;
  }

  private static void prop(Writer w, String key, Object value, boolean comma, int level) throws Exception {
    w.write(indent(level) + quote(key) + ": ");
    if (value instanceof Number || value instanceof Boolean) {
      w.write(String.valueOf(value));
    } else {
      w.write(quote(String.valueOf(value)));
    }
    if (comma) w.write(",");
    w.write("\n");
  }

  private static void inlineProp(Writer w, String key, Object value, boolean comma) throws Exception {
    w.write(quote(key) + ": ");
    if (value instanceof Number || value instanceof Boolean) {
      w.write(String.valueOf(value));
    } else {
      w.write(quote(String.valueOf(value)));
    }
    if (comma) w.write(", ");
  }

  private static String quote(String value) {
    StringBuilder sb = new StringBuilder(value.length() + 2);
    sb.append('"');
    for (int i = 0; i < value.length(); i++) {
      char ch = value.charAt(i);
      if (Character.isHighSurrogate(ch)) {
        if (i + 1 < value.length() && Character.isLowSurrogate(value.charAt(i + 1))) {
          sb.append(ch).append(value.charAt(i + 1));
          i++;
        } else {
          sb.append("\\ufffd");
        }
        continue;
      }
      if (Character.isLowSurrogate(ch)) {
        sb.append("\\ufffd");
        continue;
      }
      switch (ch) {
        case '\\' -> sb.append("\\\\");
        case '"' -> sb.append("\\\"");
        case '\b' -> sb.append("\\b");
        case '\f' -> sb.append("\\f");
        case '\n' -> sb.append("\\n");
        case '\r' -> sb.append("\\r");
        case '\t' -> sb.append("\\t");
        default -> {
          if (ch < 0x20 || ch == '\u2028' || ch == '\u2029') sb.append(String.format("\\u%04x", (int) ch));
          else sb.append(ch);
        }
      }
    }
    sb.append('"');
    return sb.toString();
  }

  private static String indent(int level) {
    return "  ".repeat(level);
  }
}

final class MceBlobDiff {
  private static final int CONTEXT = 32;

  public static void main(String[] args) throws Exception {
    if (args.length < 3) {
      System.err.println("Usage: java MceBlobDiff <before.mce> <after.mce> <outDir>");
      System.exit(2);
    }
    Path outDir = Path.of(args[2]);
    Files.createDirectories(outDir);
    Map<String, BlobRow> before = load(Path.of(args[0]));
    Map<String, BlobRow> after = load(Path.of(args[1]));
    try (Writer w = Files.newBufferedWriter(outDir.resolve("blob_diff.json"), StandardCharsets.UTF_8)) {
      w.write("[\n");
      int written = 0;
      Set<String> keys = new LinkedHashSet<>();
      keys.addAll(before.keySet());
      keys.addAll(after.keySet());
      for (String key : keys) {
        BlobRow a = before.get(key);
        BlobRow b = after.get(key);
        if (a == null || b == null) continue;
        if (MessageDigest.isEqual(a.bytes, b.bytes)) continue;
        if (written++ > 0) w.write(",\n");
        writeEntry(w, a, b);
      }
      w.write("\n]\n");
    }
  }

  private static Map<String, BlobRow> load(Path file) throws Exception {
    Map<String, BlobRow> rows = new java.util.LinkedHashMap<>();
    try (Database db = DatabaseBuilder.open(file.toFile())) {
      for (String tableName : db.getTableNames()) {
        Table table = db.getTable(tableName);
        for (Row row : table) {
          String rowKey = firstCell(row);
          for (Column c : table.getColumns()) {
            Object value = row.get(c.getName());
            if (!(value instanceof byte[] bytes)) continue;
            if (!isLikelyCanvasObjectBlob(tableName, c.getName())) continue;
            String key = tableName + "|" + c.getName() + "|" + rowKey;
            rows.put(key, new BlobRow(tableName, c.getName(), rowKey, bytes));
          }
        }
      }
    }
    return rows;
  }

  private static boolean isLikelyCanvasObjectBlob(String tableName, String columnName) {
    if (!"lbObjects".equalsIgnoreCase(columnName)) return false;
    return "WndUser".equalsIgnoreCase(tableName) || "WndDevice".equalsIgnoreCase(tableName);
  }

  private static void writeEntry(Writer w, BlobRow before, BlobRow after) throws Exception {
    w.write("  {\n");
    prop(w, "table", after.table, true, 2);
    prop(w, "column", after.column, true, 2);
    prop(w, "rowKey", after.rowKey, true, 2);
    prop(w, "beforeBytes", before.bytes.length, true, 2);
    prop(w, "afterBytes", after.bytes.length, true, 2);
    prop(w, "beforeSha256", sha256Hex(before.bytes), true, 2);
    prop(w, "afterSha256", sha256Hex(after.bytes), true, 2);
    w.write("    \"changedRanges\": [");
    List<Range> ranges = changedRanges(before.bytes, after.bytes);
    for (int i = 0; i < ranges.size() && i < 200; i++) {
      if (i > 0) w.write(", ");
      Range r = ranges.get(i);
      int start = Math.max(0, r.start - CONTEXT);
      int end = Math.min(Math.max(before.bytes.length, after.bytes.length), r.end + CONTEXT);
      w.write("{");
      inlineProp(w, "start", hex(r.start), true);
      inlineProp(w, "endExclusive", hex(r.end), true);
      inlineProp(w, "length", r.end - r.start, true);
      inlineProp(w, "windowStart", hex(start), true);
      inlineProp(w, "windowEndExclusive", hex(end), true);
      inlineProp(w, "beforeHex", hexWindow(before.bytes, start, end), true);
      inlineProp(w, "afterHex", hexWindow(after.bytes, start, end), false);
      w.write("}");
    }
    w.write("]\n");
    w.write("  }");
  }

  private static List<Range> changedRanges(byte[] before, byte[] after) {
    List<Range> raw = new ArrayList<>();
    int max = Math.max(before.length, after.length);
    int start = -1;
    for (int i = 0; i < max; i++) {
      boolean same = i < before.length && i < after.length && before[i] == after[i];
      if (!same && start < 0) start = i;
      if ((same || i == max - 1) && start >= 0) {
        int end = same ? i : i + 1;
        raw.add(new Range(start, end));
        start = -1;
      }
    }
    List<Range> merged = new ArrayList<>();
    for (Range r : raw) {
      if (!merged.isEmpty() && r.start <= merged.get(merged.size() - 1).end + 16) {
        Range last = merged.remove(merged.size() - 1);
        merged.add(new Range(last.start, Math.max(last.end, r.end)));
      } else {
        merged.add(r);
      }
    }
    return merged;
  }

  private static String hexWindow(byte[] bytes, int start, int end) {
    StringBuilder sb = new StringBuilder(Math.max(0, end - start) * 2);
    for (int i = start; i < end && i < bytes.length; i++) {
      sb.append(String.format("%02x", bytes[i] & 0xFF));
    }
    return sb.toString();
  }

  private static String firstCell(Row row) {
    for (Object value : row.values()) {
      if (value != null) return String.valueOf(value);
    }
    return "";
  }

  private static void prop(Writer w, String key, Object value, boolean comma, int level) throws Exception {
    w.write("  ".repeat(level) + quote(key) + ": ");
    if (value instanceof Number || value instanceof Boolean) w.write(String.valueOf(value));
    else w.write(quote(String.valueOf(value)));
    if (comma) w.write(",");
    w.write("\n");
  }

  private static void inlineProp(Writer w, String key, Object value, boolean comma) throws Exception {
    w.write(quote(key) + ": ");
    if (value instanceof Number || value instanceof Boolean) w.write(String.valueOf(value));
    else w.write(quote(String.valueOf(value)));
    if (comma) w.write(", ");
  }

  private static String sha256Hex(byte[] bytes) throws Exception {
    MessageDigest digest = MessageDigest.getInstance("SHA-256");
    byte[] hash = digest.digest(bytes);
    StringBuilder sb = new StringBuilder(hash.length * 2);
    for (byte b : hash) sb.append(String.format("%02x", b & 0xFF));
    return sb.toString();
  }

  private static String hex(int value) {
    return "0x" + Integer.toHexString(value).toUpperCase();
  }

  private static String quote(String value) {
    StringBuilder sb = new StringBuilder(value.length() + 2);
    sb.append('"');
    for (int i = 0; i < value.length(); i++) {
      char ch = value.charAt(i);
      if (Character.isHighSurrogate(ch)) {
        if (i + 1 < value.length() && Character.isLowSurrogate(value.charAt(i + 1))) {
          sb.append(ch).append(value.charAt(i + 1));
          i++;
        } else {
          sb.append("\\ufffd");
        }
        continue;
      }
      if (Character.isLowSurrogate(ch)) {
        sb.append("\\ufffd");
        continue;
      }
      switch (ch) {
        case '\\' -> sb.append("\\\\");
        case '"' -> sb.append("\\\"");
        case '\b' -> sb.append("\\b");
        case '\f' -> sb.append("\\f");
        case '\n' -> sb.append("\\n");
        case '\r' -> sb.append("\\r");
        case '\t' -> sb.append("\\t");
        default -> {
          if (ch < 0x20 || ch == '\u2028' || ch == '\u2029') sb.append(String.format("\\u%04x", (int) ch));
          else sb.append(ch);
        }
      }
    }
    sb.append('"');
    return sb.toString();
  }

  private record BlobRow(String table, String column, String rowKey, byte[] bytes) {}
  private record Range(int start, int end) {}
}

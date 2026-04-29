import com.healthmarketscience.jackcess.Column;
import com.healthmarketscience.jackcess.Database;
import com.healthmarketscience.jackcess.DatabaseBuilder;
import com.healthmarketscience.jackcess.Row;
import com.healthmarketscience.jackcess.Table;

import java.io.File;
import java.io.Writer;
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
      switch (ch) {
        case '\\' -> sb.append("\\\\");
        case '"' -> sb.append("\\\"");
        case '\b' -> sb.append("\\b");
        case '\f' -> sb.append("\\f");
        case '\n' -> sb.append("\\n");
        case '\r' -> sb.append("\\r");
        case '\t' -> sb.append("\\t");
        default -> {
          if (ch < 0x20) sb.append(String.format("\\u%04x", (int) ch));
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

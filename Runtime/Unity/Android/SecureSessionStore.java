package com.learningwithliann.mahjongtones;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Called by ProtectedAccountStore. The AES key never leaves Android Keystore. */
public final class SecureSessionStore {
    private static final String PREFS = "liann.mahjongtones.account.v2";
    private static final String ITEM = "session";
    private static final String ALIAS = "liann.mahjongtones.session.v2";

    private SecureSessionStore() {}

    private static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    private static SecretKey key() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        SecretKey existing = (SecretKey) store.getKey(ALIAS, null);
        if (existing != null) return existing;
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build());
        return generator.generateKey();
    }

    public static boolean save(Context context, String json) {
        if (context == null || json == null) return false;
        try {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, key());
            byte[] iv = cipher.getIV();
            byte[] encrypted = cipher.doFinal(json.getBytes(StandardCharsets.UTF_8));
            ByteBuffer buffer = ByteBuffer.allocate(2 + iv.length + encrypted.length);
            buffer.put((byte) 1).put((byte) iv.length).put(iv).put(encrypted);
            return prefs(context).edit().putString(ITEM,
                    Base64.encodeToString(buffer.array(), Base64.NO_WRAP)).commit();
        } catch (Exception e) {
            return false;
        }
    }

    public static String load(Context context) {
        if (context == null) return null;
        String stored = prefs(context).getString(ITEM, null);
        if (stored == null) return null;
        try {
            ByteBuffer buffer = ByteBuffer.wrap(Base64.decode(stored, Base64.NO_WRAP));
            if (buffer.remaining() < 2 || buffer.get() != 1) throw new IllegalArgumentException("version");
            int ivLength = buffer.get() & 0xff;
            if (ivLength != 12 || buffer.remaining() < ivLength + 16) throw new IllegalArgumentException("length");
            byte[] iv = new byte[ivLength];
            buffer.get(iv);
            byte[] encrypted = new byte[buffer.remaining()];
            buffer.get(encrypted);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(128, iv));
            return new String(cipher.doFinal(encrypted), StandardCharsets.UTF_8);
        } catch (Exception e) {
            // Restored backups can contain ciphertext without the device-bound key.
            delete(context);
            return null;
        }
    }

    public static boolean delete(Context context) {
        return context != null && prefs(context).edit().remove(ITEM).commit();
    }
}


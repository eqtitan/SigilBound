// Serves the downloaded update APK to Android's package installer (Client/Updater.cs, PLAN step 34):
// the fallback install route, the same screen as installing an APK from a file manager. A minimal
// read-only provider for the one file in the app cache, so it needs no androidx or XML resources.
// Declared in the manifest by Editor/AndroidUpdatePermission.cs (authority <package>.sbupdate).
package com.sigilbound.updater;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;

public final class SigilboundApkProvider extends ContentProvider {
    static final String FILE = "sigilbound-update.apk";

    File apk() { return new File(getContext().getCacheDir(), FILE); }

    @Override public boolean onCreate() { return true; }

    @Override public String getType(Uri uri) { return "application/vnd.android.package-archive"; }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        File f = apk();
        if (!f.exists()) throw new FileNotFoundException(f.getPath());
        return ParcelFileDescriptor.open(f, ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sort) {
        File f = apk();
        MatrixCursor c = new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE });
        c.addRow(new Object[] { FILE, f.length() });
        return c;
    }

    @Override public Uri insert(Uri uri, ContentValues values) { return null; }
    @Override public int delete(Uri uri, String selection, String[] args) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { return 0; }
}

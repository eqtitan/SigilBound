// Android in-app updates (docs/sigilbound/PLAN.md step 34), called from Client/Updater.cs.
// Installs a downloaded APK through PackageInstaller, so the game becomes its own installer of
// record. On Android 12+ (API 31) it then asks for USER_ACTION_NOT_REQUIRED, and later updates can
// install without a prompt; Android decides, and shows its own confirmation when it insists.
// Needs the REQUEST_INSTALL_PACKAGES permission (added by Editor/AndroidUpdatePermission.cs) and
// the one-time "Install unknown apps" allowance for Sigilbound.
package com.sigilbound.updater;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageInstaller;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;

public final class SigilboundUpdateInstaller {
    static final String ACTION = "com.sigilbound.updater.INSTALL_STATUS";

    // "" idle, "working", "user-action" (Android's confirm screen is up), "success", or
    // "failed:<PackageInstaller status code>:<message>" (code 3 = the user cancelled; -1 = an exception here).
    static volatile String status = "";
    static BroadcastReceiver receiver;

    public static String getStatus() { return status; }

    // Whether Android lets this app install packages ("Install unknown apps" for Sigilbound).
    public static boolean canInstall(Activity activity) {
        if (Build.VERSION.SDK_INT < 26) return true;
        return activity.getPackageManager().canRequestPackageInstalls();
    }

    // Opens the "Install unknown apps" page for Sigilbound.
    public static void openPermissionSettings(Activity activity) {
        try {
            Intent i = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + activity.getPackageName()));
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(i);
        } catch (Throwable t) {
            status = "failed:-1:" + t;
        }
    }

    // Starts the install on a background thread; poll getStatus() for the outcome.
    public static void install(final Activity activity, final String apkPath) {
        status = "working";
        new Thread(new Runnable() {
            public void run() {
                try { doInstall(activity, apkPath); }
                catch (Throwable t) { status = "failed:-1:" + t; }
            }
        }).start();
    }

    static void doInstall(Activity activity, String apkPath) throws Exception {
        Context app = activity.getApplicationContext();
        PackageInstaller installer = app.getPackageManager().getPackageInstaller();
        PackageInstaller.SessionParams params = new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
        params.setAppPackageName(app.getPackageName());
        if (Build.VERSION.SDK_INT >= 31) params.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED);
        File apk = new File(apkPath);
        params.setSize(apk.length());
        int id = installer.createSession(params);
        PackageInstaller.Session session = installer.openSession(id);
        try {
            InputStream in = new FileInputStream(apk);
            OutputStream out = session.openWrite("base.apk", 0, apk.length());
            try {
                byte[] buf = new byte[1 << 16];
                int n;
                while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                session.fsync(out);
            } finally {
                out.close();
                in.close();
            }
            registerReceiver(app);
            Intent callback = new Intent(ACTION).setPackage(app.getPackageName());
            int flags = PendingIntent.FLAG_UPDATE_CURRENT | (Build.VERSION.SDK_INT >= 31 ? PendingIntent.FLAG_MUTABLE : 0);
            PendingIntent pending = PendingIntent.getBroadcast(app, id, callback, flags);
            session.commit(pending.getIntentSender());
        } finally {
            session.close();
        }
    }

    static synchronized void registerReceiver(Context app) {
        if (receiver != null) return;
        receiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context ctx, Intent intent) {
                int st = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
                if (st == PackageInstaller.STATUS_PENDING_USER_ACTION) {
                    Intent confirm = (Intent) intent.getParcelableExtra(Intent.EXTRA_INTENT);
                    if (confirm != null) {
                        confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        ctx.startActivity(confirm);
                    }
                    status = "user-action";
                } else if (st == PackageInstaller.STATUS_SUCCESS) {
                    status = "success";
                } else {
                    status = "failed:" + st + ":" + intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);
                }
            }
        };
        IntentFilter filter = new IntentFilter(ACTION);
        if (Build.VERSION.SDK_INT >= 33) app.registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED);
        else app.registerReceiver(receiver, filter);
    }
}

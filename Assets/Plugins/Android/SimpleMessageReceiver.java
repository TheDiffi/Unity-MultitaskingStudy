package com.samples.passthroughcamera;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

public class SimpleMessageReceiver extends BroadcastReceiver {
    public static String lastMessage = "";

    @Override
    public void onReceive(Context context, Intent intent) {
        String data = intent.getStringExtra("data");
        Log.d("SimpleMessageReceiver", "Received message: " + data);
        lastMessage = data;
    }

    // Allow Unity to poll the last received message
    public static String getLastMessage() {
        return lastMessage;
    }
}

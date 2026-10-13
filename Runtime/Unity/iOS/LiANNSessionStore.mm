#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static NSDictionary *LMTSessionQuery(void)
{
    return @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: @"com.learningwithliann.mahjongtones.account.v2",
        (__bridge id)kSecAttrAccount: @"supabase-session"
    };
}

extern "C" {
    char *LMT_SessionLoad(void)
    {
        @autoreleasepool {
            // Keychain can outlive an app uninstall. User defaults do not, so a
            // fresh install must discard any orphaned session from an old install.
            NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
            NSString *installKey = @"liann.mahjongtones.account.v2.installed";
            if (![defaults boolForKey:installKey]) {
                SecItemDelete((__bridge CFDictionaryRef)LMTSessionQuery());
                [defaults setBool:YES forKey:installKey];
                return NULL;
            }
            NSMutableDictionary *query = [LMTSessionQuery() mutableCopy];
            query[(__bridge id)kSecReturnData] = @YES;
            query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
            CFTypeRef found = NULL;
            if (SecItemCopyMatching((__bridge CFDictionaryRef)query, &found) != errSecSuccess)
                return NULL;
            NSData *data = (__bridge_transfer NSData *)found;
            NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
            return json == nil ? NULL : strdup([json UTF8String]);
        }
    }

    bool LMT_SessionSave(const char *json)
    {
        if (json == NULL) return false;
        @autoreleasepool {
            NSData *data = [[NSString stringWithUTF8String:json] dataUsingEncoding:NSUTF8StringEncoding];
            if (data == nil) return false;
            NSDictionary *query = LMTSessionQuery();
            OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query,
                (__bridge CFDictionaryRef)@{(__bridge id)kSecValueData: data});
            if (status == errSecSuccess) return true;
            if (status != errSecItemNotFound) return false;
            NSMutableDictionary *item = [query mutableCopy];
            item[(__bridge id)kSecValueData] = data;
            item[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly;
            return SecItemAdd((__bridge CFDictionaryRef)item, NULL) == errSecSuccess;
        }
    }

    bool LMT_SessionDelete(void)
    {
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)LMTSessionQuery());
        return status == errSecSuccess || status == errSecItemNotFound;
    }

    void LMT_SessionFree(char *value)
    {
        free(value);
    }
}


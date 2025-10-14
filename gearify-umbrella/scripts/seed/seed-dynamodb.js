const { DynamoDBClient } = require('@aws-sdk/client-dynamodb');
const { DynamoDBDocumentClient, PutCommand } = require('@aws-sdk/lib-dynamodb');

const client = new DynamoDBClient({
  region: 'us-east-1',
  endpoint: 'http://localhost:4566',
  credentials: {
    accessKeyId: 'test',
    secretAccessKey: 'test'
  }
});

const docClient = DynamoDBDocumentClient.from(client);

async function seedTenants() {
  const tenants = [
    {
      tenantId: 'default',
      name: 'Gearify Official',
      domain: 'localhost',
      theme: {
        primaryColor: '#1976d2',
        logoUrl: 'https://placehold.co/200x60/1976d2/white?text=Gearify'
      },
      createdAt: new Date().toISOString()
    },
    {
      tenantId: 'global-demo',
      name: 'Global Cricket Demo',
      domain: 'demo.localhost',
      theme: {
        primaryColor: '#388e3c',
        logoUrl: 'https://placehold.co/200x60/388e3c/white?text=Demo'
      },
      createdAt: new Date().toISOString()
    }
  ];

  for (const tenant of tenants) {
    await docClient.send(new PutCommand({
      TableName: 'gearify-tenants',
      Item: tenant
    }));
    console.log(`✓ Seeded tenant: ${tenant.tenantId}`);
  }
}

async function seedProducts() {
  const products = [
    { name: 'English Willow Bat', category: 'bats', price: 299.99, weight: 1180, grade: 'Grade 1', weightType: 'Medium' },
    { name: 'Kashmir Willow Bat', category: 'bats', price: 89.99, weight: 1200, grade: 'Grade 2', weightType: 'Heavy' },
    { name: 'Batting Pads Professional', category: 'pads', price: 79.99, weight: 850, grade: 'Premium', weightType: 'Light' },
    { name: 'Batting Gloves Pro', category: 'gloves', price: 49.99, weight: 200, grade: 'Premium', weightType: 'Light' },
    { name: 'Cricket Ball Red Leather', category: 'balls', price: 19.99, weight: 160, grade: 'Match', weightType: 'Standard' },
    { name: 'Helmet Pro Series', category: 'helmets', price: 129.99, weight: 650, grade: 'Premium', weightType: 'Medium' }
  ];

  for (let i = 0; i < products.length; i++) {
    const product = products[i];
    for (const tenantId of ['default', 'global-demo']) {
      await docClient.send(new PutCommand({
        TableName: 'gearify-products',
        Item: {
          tenantId,
          productId: `prod-${tenantId}-${i + 1}`,
          ...product,
          stock: Math.floor(Math.random() * 100) + 10,
          addOns: ['Knocking', 'Oiling', 'Toe Binding'],
          createdAt: new Date().toISOString()
        }
      }));
    }
    console.log(`✓ Seeded product: ${product.name}`);
  }
}

async function seedFeatureFlags() {
  const flags = [
    { flagKey: 'enable-checkout', enabled: true },
    { flagKey: 'enable-paypal', enabled: true },
    { flagKey: 'enable-stripe', enabled: true }
  ];

  for (const flag of flags) {
    for (const tenantId of ['default', 'global-demo']) {
      await docClient.send(new PutCommand({
        TableName: 'gearify-feature-flags',
        Item: {
          tenantId,
          ...flag,
          updatedAt: new Date().toISOString()
        }
      }));
    }
    console.log(`✓ Seeded flag: ${flag.flagKey}`);
  }
}

(async () => {
  try {
    await seedTenants();
    await seedProducts();
    await seedFeatureFlags();
    console.log('✅ DynamoDB seeding complete');
  } catch (err) {
    console.error('❌ Seeding failed:', err);
    process.exit(1);
  }
})();
